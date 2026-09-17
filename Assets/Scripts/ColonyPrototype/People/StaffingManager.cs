using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AsteroidColony
{
    public enum AssignmentResult
    {
        Applied,
        RejectedColonistUnknown,
        RejectedWorkplaceInvalid,
        RejectedRoleInvalid,
        RejectedRoleNotOffered,
        RejectedShiftUnknown,
        RejectedMissingClass,
        RejectedAtCapacity,
        RejectedShipInvalid,
        RejectedShipOccupied,
        RejectedMissingHome,
        RejectedCrewBaseMismatch
    }

    /// <summary>T08: owns explicit employment, shifts, fatigue, and commutes.</summary>
    public class StaffingManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public static StaffingManager Instance { get; private set; }

        [SerializeField] private int employedCount;
        [SerializeField] private string lastDiagnostic = string.Empty;
        [SerializeField, TextArea(5, 40)] private string lastScheduleDump = string.Empty;

        private readonly List<ColonistAgent> knownColonists = new List<ColonistAgent>();
        private readonly List<CollectiveCommute> commuteGroups = new List<CollectiveCommute>();

        public int EmployedCount => employedCount;
        public string LastDiagnostic => lastDiagnostic;
        public string LastScheduleDump => lastScheduleDump;
        public IReadOnlyList<ColonistAgent> KnownColonists => knownColonists;
        public int SimulationTickPriority => 100;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active StaffingManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
            DiscoverColonists();
            DiscoverShips();
            ReconcilePopulation();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
            SimulationManager.RegisterTickable(this);
            DiscoverColonists();
            DiscoverShips();
            ReconcilePopulation();
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SimulationTick(float deltaGameHours)
        {
            ReconcilePopulation();
            float hour = CurrentGameHour();
            commuteGroups.Clear();
            TickResponsibleShipDuties(Mathf.Max(0f, deltaGameHours), hour);
            for (int i = 0; i < knownColonists.Count; i++)
                TickColonist(knownColonists[i], Mathf.Max(0f, deltaGameHours), hour);
            FlushCommutes();
            UpdateCounts();
        }

        /// <summary>
        /// Builds a deterministic, read-only view of every colonist's daily
        /// assignment and current duty state. Planned off-duty time is not
        /// mislabeled as sleep; the activity column reports what is actually
        /// happening now.
        /// </summary>
        public string BuildDailyScheduleTable(float absoluteGameHour)
        {
            List<ColonistAgent> sorted = new List<ColonistAgent>();
            for (int i = 0; i < knownColonists.Count; i++)
            {
                ColonistAgent colonist = knownColonists[i];
                if (colonist != null && colonist.isActiveAndEnabled)
                    sorted.Add(colonist);
            }
            sorted.Sort(CompareColonistsForSchedule);

            StringBuilder table = new StringBuilder();
            table.AppendLine($"Daily work/off-duty schedule ({SimulationTime.FormatTimestamp(absoluteGameHour)}, 24h cycle)");
            table.AppendLine("Colonist | Home | Workplace | Role | Shift | Work | Off duty | Now | Duty state | Fatigue | Exhausted | Last duty");
            for (int i = 0; i < sorted.Count; i++)
            {
                ColonistAgent colonist = sorted[i];
                EmploymentAssignment employment = colonist.currentEmployment;
                ColonistStatusComponent status = colonist.GetComponent<ColonistStatusComponent>();
                string colonistName = ColonistLabel(colonist);
                string home = AnchorLabel(colonist.home);
                string activity = colonist.activity.ToString();
                string dutyState = status != null ? status.CurrentDutyState.ToString() : "-";
                string fatigue = status != null ? status.Fatigue.ToString("0.00") : "-";
                string exhausted = status != null ? status.IsExhausted.ToString() : "-";

                if (employment == null || !employment.IsAssigned)
                {
                    table.AppendLine($"{colonistName} | {home} | - | - | - | none | all day | {activity} | {dutyState} | {fatigue} | {exhausted} | {FormatLastDuty(status)}");
                    continue;
                }

                StaffingComponent workplace = employment.workplace;
                ShipComponent ship = workplace != null ? workplace.GetComponent<ShipComponent>() : null;
                string workplaceName = ship != null ? ShipLabel(ship) : workplace != null ? workplace.DisplayName : "-";
                string role = employment.role != null ? RoleLabel(employment.role) : "-";
                ShiftDefinition shift = workplace != null && workplace.shiftPattern != null
                    ? workplace.shiftPattern.FindShift(employment.shiftId) : null;
                string shiftName = string.IsNullOrEmpty(employment.shiftId) ? "-" : employment.shiftId;
                string workWindow = FormatWorkWindow(shift);
                string offDutyWindow = FormatOffDutyWindow(shift);
                table.AppendLine($"{colonistName} | {home} | {workplaceName} | {role} | {shiftName} | {workWindow} | {offDutyWindow} | {activity} | {dutyState} | {fatigue} | {exhausted} | {FormatLastDuty(status)}");
            }

            return table.ToString().TrimEnd();
        }

        [ContextMenu("Dump Daily Work/Off-Duty Schedule")]
        private void DumpDailySchedule()
        {
            lastScheduleDump = BuildDailyScheduleTable(CurrentGameHour());
            Debug.Log(lastScheduleDump, this);
        }

        public void RegisterColonist(ColonistAgent colonist)
        {
            if (colonist != null && colonist.isActiveAndEnabled && !knownColonists.Contains(colonist))
                knownColonists.Add(colonist);
        }

        public void UnregisterColonist(ColonistAgent colonist)
        {
            if (colonist != null)
                knownColonists.Remove(colonist);
        }

        public void ReconcilePopulation()
        {
            if (PopulationManager.Instance != null)
            {
                IReadOnlyList<ColonistAgent> colonists = PopulationManager.Instance.Colonists;
                for (int i = 0; i < colonists.Count; i++)
                    RegisterColonist(colonists[i]);
            }
            for (int i = knownColonists.Count - 1; i >= 0; i--)
                if (knownColonists[i] == null || !knownColonists[i].isActiveAndEnabled)
                    knownColonists.RemoveAt(i);
        }

        private void DiscoverShips()
        {
            ShipComponent[] found = FindObjectsByType<ShipComponent>();
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null)
                    found[i].EnsureInitialized();
        }

        private void DiscoverColonists()
        {
            ColonistAgent[] found = FindObjectsByType<ColonistAgent>();
            for (int i = 0; i < found.Length; i++)
                RegisterColonist(found[i]);
        }

        public AssignmentResult Assign(
            ColonistAgent colonist, StaffingComponent workplace, StaffingRoleDefinition role, string shiftId)
        {
            AssignmentResult validation = ValidateAssignment(colonist, workplace, role, shiftId);
            if (validation != AssignmentResult.Applied)
            {
                ShipComponent requestedShip = workplace != null ? workplace.GetComponent<ShipComponent>() : null;
                string subject = colonist == null ? "<unknown colonist>" :
                    (string.IsNullOrEmpty(colonist.displayName) ? colonist.name : colonist.displayName);
                string workplaceName = workplace == null ? "<none>" : workplace.DisplayName;
                string roleName = role == null ? "<none>" :
                    (string.IsNullOrEmpty(role.displayName) ? role.name : role.displayName);
                string baseText = requestedShip == null ? string.Empty :
                    $" expected base {AnchorLabel(requestedShip.crewChangeBase)}, actual home {AnchorLabel(colonist != null ? colonist.home : null)}";
                SetDiagnostic($"assignment rejected for {subject}: {workplaceName} / {roleName} / {shiftId ?? "<none>"} - {Describe(validation)}{baseText}");
                return validation;
            }

            RegisterColonist(colonist);
            EmploymentAssignment current = colonist.currentEmployment;
            ShipComponent targetShip = workplace.GetComponent<ShipComponent>();
            if (targetShip != null && targetShip.crewChangeBase == null)
                targetShip.crewChangeBase = colonist.home;
            if (current != null && current.workplace == workplace && current.role == role && current.shiftId == shiftId)
                return AssignmentResult.Applied;

            ColonistStatusComponent status = EnsureStatus(colonist);
            ReleasePreviousEmployment(colonist, current, status, DutyEndReason.Reassigned);
            colonist.currentEmployment = new EmploymentAssignment(workplace, role, shiftId);
            if (colonist.activity == ColonistActivity.Working)
                SetActivity(colonist, ColonistActivity.Idle);
            Log($"{colonist.displayName} assigned to {colonist.currentEmployment.Describe()}");
            UpdateCounts();
            commuteGroups.Clear();
            ReconcileColonist(colonist, CurrentGameHour());
            FlushCommutes();
            return AssignmentResult.Applied;
        }

        public AssignmentResult Unassign(ColonistAgent colonist)
        {
            if (colonist == null)
                return AssignmentResult.RejectedColonistUnknown;
            RegisterColonist(colonist);
            if (colonist.currentEmployment == null)
                return AssignmentResult.Applied;

            EmploymentAssignment previous = colonist.currentEmployment;
            ColonistStatusComponent status = EnsureStatus(colonist);
            ReleasePreviousEmployment(colonist, previous, status, DutyEndReason.Unassigned);
            colonist.currentEmployment = null;
            if (!HasActivePassengerContract(colonist))
                SetActivity(colonist, ColonistActivity.Idle);
            Log($"{colonist.displayName} unassigned");
            UpdateCounts();
            commuteGroups.Clear();
            ReconcileColonist(colonist, CurrentGameHour());
            FlushCommutes();
            return AssignmentResult.Applied;
        }

        private AssignmentResult ValidateAssignment(
            ColonistAgent colonist, StaffingComponent workplace, StaffingRoleDefinition role, string shiftId)
        {
            if (colonist == null)
                return AssignmentResult.RejectedColonistUnknown;
            if (workplace == null || workplace.workplaceLocation == null || workplace.shiftPattern == null)
                return AssignmentResult.RejectedWorkplaceInvalid;
            if (role == null)
                return AssignmentResult.RejectedRoleInvalid;
            if (!workplace.OffersRole(role))
                return AssignmentResult.RejectedRoleNotOffered;
            if (role.requiredClass == null || role.maximumAssignedPerShift < 1 ||
                role.minimumActiveForOperation < 0 || role.minimumActiveForOperation > role.maximumAssignedPerShift ||
                role.exertionMultiplier < 0f || float.IsNaN(role.exertionMultiplier) || float.IsInfinity(role.exertionMultiplier))
                return AssignmentResult.RejectedRoleInvalid;
            if (!workplace.shiftPattern.HasShift(shiftId))
                return AssignmentResult.RejectedShiftUnknown;
            if (!colonist.HasClass(role.requiredClass))
                return AssignmentResult.RejectedMissingClass;
            ShipComponent ship = workplace.GetComponent<ShipComponent>();
            if (ship != null)
            {
                ship.EnsureInitialized();
                if (ship.crewStaffing != workplace || ship.operatingRole != role || ship.ShipLocation == null)
                    return AssignmentResult.RejectedShipInvalid;
                if (colonist.home == null)
                    return AssignmentResult.RejectedMissingHome;
                if (ship.crewChangeBase != null && ship.crewChangeBase != colonist.home)
                    return AssignmentResult.RejectedCrewBaseMismatch;
            }
            if (CountAssigned(workplace, role, shiftId, colonist) >= role.maximumAssignedPerShift)
                return AssignmentResult.RejectedAtCapacity;
            return AssignmentResult.Applied;
        }

        public IReadOnlyList<ColonistAgent> GetEligibleColonists(StaffingComponent workplace, StaffingRoleDefinition role)
        {
            List<ColonistAgent> eligible = new List<ColonistAgent>();
            if (role == null || role.requiredClass == null)
                return eligible;
            for (int i = 0; i < knownColonists.Count; i++)
                if (knownColonists[i] != null && knownColonists[i].isActiveAndEnabled &&
                    knownColonists[i].HasClass(role.requiredClass))
                    eligible.Add(knownColonists[i]);
            return eligible;
        }

        public IReadOnlyList<ColonistAgent> GetAssignedWorkers(StaffingComponent workplace, StaffingRoleDefinition role, string shiftId)
        {
            List<ColonistAgent> assigned = new List<ColonistAgent>();
            CollectAssignedWorkers(workplace, role, shiftId, assigned);
            return assigned;
        }

        public void CollectAssignedWorkers(StaffingComponent workplace, StaffingRoleDefinition role, string shiftId, List<ColonistAgent> buffer)
        {
            if (buffer == null)
                return;
            for (int i = 0; i < knownColonists.Count; i++)
            {
                ColonistAgent colonist = knownColonists[i];
                EmploymentAssignment employment = colonist != null ? colonist.currentEmployment : null;
                if (colonist == null || !colonist.isActiveAndEnabled || employment == null ||
                    !employment.IsAssigned || employment.workplace != workplace || employment.role != role)
                    continue;
                if (!string.IsNullOrEmpty(shiftId) && employment.shiftId != shiftId)
                    continue;
                buffer.Add(colonist);
            }
        }

        public static string Describe(AssignmentResult result)
        {
            switch (result)
            {
                case AssignmentResult.Applied: return "applied";
                case AssignmentResult.RejectedColonistUnknown: return "colonist unknown";
                case AssignmentResult.RejectedWorkplaceInvalid: return "workplace invalid";
                case AssignmentResult.RejectedRoleInvalid: return "role invalid";
                case AssignmentResult.RejectedRoleNotOffered: return "role not offered by workplace";
                case AssignmentResult.RejectedShiftUnknown: return "shift not in workplace pattern";
                case AssignmentResult.RejectedMissingClass: return "colonist lacks the required class";
                case AssignmentResult.RejectedAtCapacity: return "role shift is at capacity";
                case AssignmentResult.RejectedShipInvalid: return "ship pilot authoring is invalid";
                case AssignmentResult.RejectedShipOccupied: return "ship already has another active pilot";
                case AssignmentResult.RejectedMissingHome: return "ship crew member has no home habitat";
                case AssignmentResult.RejectedCrewBaseMismatch: return "ship crew member home does not match the crew-change base";
                default: return result.ToString();
            }
        }

        private void TickColonist(ColonistAgent colonist, float deltaGameHours, float hour)
        {
            if (colonist == null)
                return;
            ColonistStatusComponent status = EnsureStatus(colonist);
            EmploymentAssignment employment = colonist.currentEmployment;
            if (IsShipEmployment(employment))
            {
                TickPilot(colonist, employment, status, deltaGameHours, hour);
                ReconcileColonist(colonist, hour);
                return;
            }
            if (colonist.activity == ColonistActivity.Working && employment != null && employment.role != null)
            {
                status.SetDutyState(ColonistDutyState.AcceptingNewWork);
                status.BeginDuty(employment.workplace, employment.role, employment.shiftId, Mathf.Max(0f, hour - deltaGameHours));
                status.AddWorkedTime(deltaGameHours);
                status.ApplyWorkFatigue(deltaGameHours, employment.role.exertionMultiplier);
                if (status.IsExhausted)
                {
                    status.EndDuty(hour, DutyEndReason.Exhausted);
                    SetActivity(colonist, ColonistActivity.Idle);
                }
            }
            else if (colonist.activity == ColonistActivity.Sleeping && colonist.currentLocation == colonist.home)
            {
                status.SetDutyState(ColonistDutyState.ReleasedResting);
                status.ApplySleepRecovery(deltaGameHours, HomeRestfulness(colonist));
            }
            else if (colonist.activity == ColonistActivity.Resting)
                status.SetDutyState(ColonistDutyState.ReleasedResting);
            ReconcileColonist(colonist, hour);
        }

        private void TickPilot(ColonistAgent colonist, EmploymentAssignment employment,
            ColonistStatusComponent status, float deltaGameHours, float hour)
        {
            ShipComponent ship = ShipForEmployment(employment);
            bool aboard = ship != null && ship.ResponsiblePilot == colonist && ship.IsPilotAboard;
            if (aboard && employment.role != null)
            {
                status.BeginPilotDuty(ship, employment.role, employment.shiftId,
                    Mathf.Max(0f, hour - deltaGameHours));
                status.SetDutyState(status.ActiveDuty != null && status.ActiveDuty.releaseRequested
                    ? (ship.ReleaseRequested && !HasActiveShipOperation(ship)
                        ? ColonistDutyState.ReturningHome
                        : ColonistDutyState.CompletingCommittedWork)
                    : ColonistDutyState.AcceptingNewWork);
                SetActivity(colonist, ColonistActivity.OnDutyCrew);
            }
            else if (colonist.activity == ColonistActivity.Sleeping && colonist.currentLocation == colonist.home)
            {
                status.SetDutyState(ColonistDutyState.ReleasedResting);
                status.ApplySleepRecovery(deltaGameHours, HomeRestfulness(colonist));
            }
        }

        private void ReconcileColonist(ColonistAgent colonist, float hour)
        {
            if (colonist == null)
                return;
            ColonistStatusComponent status = EnsureStatus(colonist);
            if (HasPassengerInTransit(colonist))
            {
                EmploymentAssignment passengerEmployment = colonist.currentEmployment;
                bool scheduled = passengerEmployment != null && passengerEmployment.IsAssigned &&
                    passengerEmployment.workplace != null && passengerEmployment.workplace.shiftPattern != null &&
                    passengerEmployment.shiftId != null &&
                    passengerEmployment.workplace.shiftPattern.IsShiftActive(passengerEmployment.shiftId, hour) &&
                    !status.IsExhausted;
                status.SetDutyState(scheduled ? ColonistDutyState.ScheduledShift : ColonistDutyState.ReturningHome);
                SetActivity(colonist, ColonistActivity.Passenger);
                return;
            }

            EmploymentAssignment employment = colonist.currentEmployment;
            LocationAnchor home = colonist.home;
            if (IsShipEmployment(employment))
            {
                ReconcilePilot(colonist, employment, home, hour);
                return;
            }

            // A reassigned or unassigned pilot remains the responsible crew of
            // the old ship until its accepted trip and return flight finish.
            ShipComponent returningShip = FindReturningShip(colonist);
            if (returningShip != null)
            {
                status.SetDutyState(HasActiveShipOperation(returningShip)
                    ? ColonistDutyState.CompletingCommittedWork
                    : ColonistDutyState.ReturningHome);
                SetActivity(colonist, ColonistActivity.OnDutyCrew);
                return;
            }
            if (employment == null || !employment.IsAssigned || employment.workplace == null ||
                employment.workplace.workplaceLocation == null || employment.workplace.shiftPattern == null ||
                employment.role == null || employment.role.requiredClass == null || !colonist.HasClass(employment.role.requiredClass))
            {
                EndDutyIfActive(colonist, hour, DutyEndReason.WorkplaceUnavailable);
                RouteHomeOrRest(colonist, home);
                return;
            }

            LocationAnchor workplace = employment.workplace.workplaceLocation;
            bool onDuty = employment.workplace.shiftPattern.IsShiftActive(employment.shiftId, hour);
            bool canWork = onDuty && !status.IsExhausted;
            LocationAnchor destination = canWork ? workplace : home;
            if (colonist.currentLocation == workplace && canWork)
            {
                status.BeginDuty(employment.workplace, employment.role, employment.shiftId, hour);
                status.SetDutyState(ColonistDutyState.AcceptingNewWork);
                SetActivity(colonist, ColonistActivity.Working);
                return;
            }
            if (destination == null)
            {
                EndDutyIfActive(colonist, hour, status.IsExhausted ? DutyEndReason.Exhausted : DutyEndReason.ShiftEnded);
                status.SetDutyState(ColonistDutyState.ReleasedResting);
                SetActivity(colonist, ColonistActivity.Idle);
                return;
            }
            if (colonist.currentLocation == destination)
            {
                EndDutyIfActive(colonist, hour, status.IsExhausted ? DutyEndReason.Exhausted : DutyEndReason.ShiftEnded);
                if (canWork)
                {
                    status.SetDutyState(ColonistDutyState.ScheduledShift);
                    SetActivity(colonist, ColonistActivity.WaitingForTransport);
                }
                else
                {
                    status.SetDutyState(colonist.currentLocation == home
                        ? ColonistDutyState.ReleasedResting
                        : ColonistDutyState.ReturningHome);
                    SetActivity(colonist, status.Fatigue > 0f || status.IsExhausted ? ColonistActivity.Sleeping : ColonistActivity.Resting);
                }
                if (canWork)
                    QueueCommute(colonist.currentLocation, destination, employment.workplace.commutePriority, colonist);
                return;
            }

            EndDutyIfActive(colonist, hour, status.IsExhausted ? DutyEndReason.Exhausted : DutyEndReason.ShiftEnded);
            status.SetDutyState(canWork ? ColonistDutyState.ScheduledShift : ColonistDutyState.ReturningHome);
            SetActivity(colonist, ColonistActivity.WaitingForTransport);
            QueueCommute(colonist.currentLocation, destination, employment.workplace.commutePriority, colonist);
        }

        private void ReconcilePilot(ColonistAgent colonist, EmploymentAssignment employment,
            LocationAnchor home, float hour)
        {
            ShipComponent ship = ShipForEmployment(employment);
            ColonistStatusComponent status = EnsureStatus(colonist);
            ShipCrewDutyComponent crew = ship != null ? ship.GetComponent<ShipCrewDutyComponent>() : null;
            if (ship != null && crew == null)
                crew = ship.gameObject.AddComponent<ShipCrewDutyComponent>();
            // Disabling the ship or its crew roster pauses the physical duty
            // state. Do not turn a temporary pause into a release request.
            if (ship != null && (!ship.isActiveAndEnabled ||
                (ship.crewStaffing != null && !ship.crewStaffing.isActiveAndEnabled)))
            {
                if (ship.ResponsiblePilot == colonist && ship.IsPilotAboard)
                    SetActivity(colonist, ColonistActivity.OnDutyCrew);
                return;
            }
            ShipComponent returningShip = FindReturningShip(colonist);
            if (returningShip != null && returningShip != ship)
            {
                status.SetDutyState(HasActiveShipOperation(returningShip)
                    ? ColonistDutyState.CompletingCommittedWork
                    : ColonistDutyState.ReturningHome);
                SetActivity(colonist, ColonistActivity.OnDutyCrew);
                return;
            }
            if (ship == null || !employment.IsAssigned || ship.ShipLocation == null ||
                ship.crewStaffing == null || ship.operatingRole == null || employment.workplace != ship.crewStaffing ||
                employment.role != ship.operatingRole || ship.crewChangeBase == null ||
                colonist.home != ship.crewChangeBase ||
                !colonist.HasClass(employment.role.requiredClass))
            {
                if (ship != null && ship.crewChangeBase != null && colonist.home != ship.crewChangeBase)
                    SetDiagnostic($"{ship.displayName}: {colonist.displayName} home/base mismatch; expected {AnchorLabel(ship.crewChangeBase)}, actual {AnchorLabel(colonist.home)}");
                if (ship != null && ship.ResponsiblePilot == colonist && ship.IsPilotAboard)
                {
                    status.RequestDutyRelease(hour, DutyEndReason.WorkplaceUnavailable);
                    ship.RequestRelease(DutyEndReason.WorkplaceUnavailable);
                    if (crew != null)
                        crew.RequestRelease(DutyEndReason.WorkplaceUnavailable);
                    SetActivity(colonist, ColonistActivity.OnDutyCrew);
                    return;
                }
                EndDutyIfActive(colonist, hour, DutyEndReason.WorkplaceUnavailable);
                RouteHomeOrRest(colonist, home);
                return;
            }

            bool onDuty = ship.crewStaffing.shiftPattern.IsShiftActive(employment.shiftId, hour);
            bool canWork = onDuty && !status.IsExhausted && ship.isActiveAndEnabled &&
                ship.crewStaffing.isActiveAndEnabled && ship.operationalEnabled && !ship.ReleaseRequested;
            if (canWork && ship.ResponsiblePilot == null && colonist.currentLocation == ship.ShipLocation)
            {
                ship.AdoptResponsiblePilot(colonist);
                status.BeginPilotDuty(ship, employment.role, employment.shiftId, hour);
                status.SetDutyState(ColonistDutyState.AcceptingNewWork);
                SetActivity(colonist, ColonistActivity.OnDutyCrew);
                return;
            }
            if (ship.ResponsiblePilot == colonist && ship.IsPilotAboard)
            {
                if (canWork)
                {
                    status.BeginPilotDuty(ship, employment.role, employment.shiftId, hour);
                    status.SetDutyState(ColonistDutyState.AcceptingNewWork);
                    SetActivity(colonist, ColonistActivity.OnDutyCrew);
                }
                else
                {
                    DutyEndReason reason = status.IsExhausted ? DutyEndReason.Exhausted :
                        (onDuty ? DutyEndReason.WorkplaceUnavailable : DutyEndReason.ShiftEnded);
                    status.RequestDutyRelease(hour, reason);
                    ship.RequestRelease(reason);
                    if (crew != null)
                        crew.RequestRelease(reason);
                    status.SetDutyState(ship.ReleaseRequested && !HasActiveShipOperation(ship)
                        ? ColonistDutyState.ReturningHome
                        : ColonistDutyState.CompletingCommittedWork);
                    SetActivity(colonist, ColonistActivity.OnDutyCrew);
                }
                return;
            }

            if (canWork && ship.CurrentDock == ship.crewChangeBase && colonist.currentLocation == ship.crewChangeBase &&
                !ship.IsTraveling && ship.ResponsiblePilot == null && crew != null)
            {
                bool boarded = crew.TryBoardEligiblePilot();
                // Boarding moves the pilot from the crew-change base to the
                // ship anchor. Stop reconciliation here; otherwise the same
                // invocation sees the newly boarded pilot at the ship anchor
                // and queues a bogus ship-to-base passenger contract.
                if (boarded && ship.ResponsiblePilot == colonist)
                    return;
            }

            if (!canWork)
            {
                EndDutyIfActive(colonist, hour, status.IsExhausted ? DutyEndReason.Exhausted : DutyEndReason.ShiftEnded);
                RouteHomeOrRest(colonist, home);
                return;
            }

            if (ship.CurrentDock == null || ship.IsTraveling)
            {
                status.SetDutyState(ColonistDutyState.ScheduledShift);
                SetActivity(colonist, ColonistActivity.WaitingForTransport);
                SetDiagnostic($"{ship.displayName}: pilot waiting for a valid dock");
                return;
            }

            status.SetDutyState(ColonistDutyState.ScheduledShift);
            SetActivity(colonist, ColonistActivity.WaitingForTransport);
            QueueCommute(colonist.currentLocation, ship.crewChangeBase, 10, colonist);
        }

        private static ShipComponent FindReturningShip(ColonistAgent colonist)
        {
            ShipComponent[] ships = FindObjectsByType<ShipComponent>();
            for (int i = 0; i < ships.Length; i++)
                if (ships[i] != null && ships[i].ReleaseRequested && ships[i].ResponsiblePilot == colonist &&
                    ships[i].IsPilotAboard)
                    return ships[i];
            return null;
        }

        private void RouteHomeOrRest(ColonistAgent colonist, LocationAnchor home)
        {
            ColonistStatusComponent status = EnsureStatus(colonist);
            if (home != null && colonist.currentLocation == home)
            {
                status.SetDutyState(ColonistDutyState.ReleasedResting);
                SetActivity(colonist, status.Fatigue > 0f || status.IsExhausted ? ColonistActivity.Sleeping : ColonistActivity.Resting);
                return;
            }
            if (home != null && colonist.currentLocation != null)
            {
                status.SetDutyState(ColonistDutyState.ReturningHome);
                SetActivity(colonist, ColonistActivity.WaitingForTransport);
                QueueCommute(colonist.currentLocation, home, 10, colonist);
            }
            else
            {
                status.SetDutyState(ColonistDutyState.ReleasedResting);
                SetActivity(colonist, ColonistActivity.Idle);
            }
        }

        private void EndDutyIfActive(ColonistAgent colonist, float hour, DutyEndReason reason)
        {
            ColonistStatusComponent status = EnsureStatus(colonist);
            if (status.HasActiveDuty)
                status.EndDuty(hour, reason);
        }

        private static ShipComponent ShipForEmployment(EmploymentAssignment employment)
        {
            return employment != null && employment.workplace != null
                ? employment.workplace.GetComponent<ShipComponent>()
                : null;
        }

        private static bool IsShipEmployment(EmploymentAssignment employment)
        {
            return ShipForEmployment(employment) != null;
        }

        private static bool HasActiveShipOperation(ShipComponent ship)
        {
            if (ship == null)
                return false;
            TransportExecutorComponent executor = ship.GetComponent<TransportExecutorComponent>();
            if (executor != null && executor.CurrentContract != null &&
                executor.CurrentContract.IsAssignedOrInFlight &&
                (executor.CurrentContract.assignedVehicle == null ||
                 executor.CurrentContract.assignedVehicle == ship.GetComponent<TransportVehicleComponent>()))
                return true;
            ExtractionMissionController extraction = ship.GetComponent<ExtractionMissionController>();
            return extraction != null && extraction.State != ExtractionMissionState.Idle;
        }

        private void TickResponsibleShipDuties(float deltaGameHours, float hour)
        {
            ShipComponent[] ships = FindObjectsByType<ShipComponent>();
            for (int i = 0; i < ships.Length; i++)
            {
                ShipComponent ship = ships[i];
                ColonistAgent pilot = ship != null ? ship.ResponsiblePilot : null;
                ShipCrewDutyComponent crew = ship != null ? ship.GetComponent<ShipCrewDutyComponent>() : null;
                if (pilot == null || !ship.isActiveAndEnabled || !ship.operationalEnabled ||
                    ship.crewStaffing == null || !ship.crewStaffing.isActiveAndEnabled ||
                    crew == null || !crew.isActiveAndEnabled ||
                    !ship.IsPilotAboard || ship.operatingRole == null)
                    continue;
                ColonistStatusComponent status = EnsureStatus(pilot);
                // A responsible pilot is a temporary physical lease. Once the
                // player reassigns that person, the old lease remains active only
                // long enough to finish the current flight; never recreate a new
                // ship duty from the person's new facility assignment.
                DutyRecord activeDuty = status.ActiveDuty;
                bool existingLease = activeDuty != null && activeDuty.isPilotDuty && activeDuty.ship == ship;
                EmploymentAssignment employment = pilot.currentEmployment;
                bool assignedToShip = IsShipEmployment(employment) && ShipForEmployment(employment) == ship;
                if (!existingLease && !assignedToShip)
                    continue;
                if (!existingLease)
                {
                    bool onDuty = ship.crewStaffing != null && ship.crewStaffing.shiftPattern != null &&
                        ship.crewStaffing.shiftPattern.IsShiftActive(employment.shiftId, hour);
                    if (!onDuty)
                        continue;
                    status.BeginPilotDuty(ship, ship.operatingRole, employment.shiftId,
                        Mathf.Max(0f, hour - deltaGameHours));
                }
                bool completingCommittedWork = ship.ReleaseRequested && HasActiveShipOperation(ship);
                status.SetDutyState(!ship.ReleaseRequested
                    ? ColonistDutyState.AcceptingNewWork
                    : (completingCommittedWork ? ColonistDutyState.CompletingCommittedWork : ColonistDutyState.ReturningHome));
                status.AddWorkedTime(deltaGameHours);
                StaffingRoleDefinition dutyRole = status.ActiveDuty != null && status.ActiveDuty.role != null
                    ? status.ActiveDuty.role : ship.operatingRole;
                status.ApplyWorkFatigue(deltaGameHours, dutyRole.exertionMultiplier);
                pilot.activity = ColonistActivity.OnDutyCrew;
                if (status.IsExhausted)
                {
                    status.RequestDutyRelease(hour, DutyEndReason.Exhausted);
                    crew.RequestRelease(DutyEndReason.Exhausted);
                }
            }
        }

        private static void ReleasePreviousEmployment(ColonistAgent colonist, EmploymentAssignment previous,
            ColonistStatusComponent status, DutyEndReason reason)
        {
            ShipComponent oldShip = ShipForEmployment(previous);
            if (oldShip != null && oldShip.ResponsiblePilot == colonist)
            {
                status.RequestDutyRelease(CurrentGameHour(), reason);
                oldShip.RequestRelease(reason);
                ShipCrewDutyComponent crew = oldShip.GetComponent<ShipCrewDutyComponent>();
                if (crew != null)
                    crew.RequestRelease(reason);
            }
            else
                status.EndDuty(CurrentGameHour(), reason);
        }

        private static void SetActivity(ColonistAgent colonist, ColonistActivity next)
        {
            if (colonist.activity != next)
                colonist.activity = next;
        }

        private static ColonistStatusComponent EnsureStatus(ColonistAgent colonist)
        {
            if (colonist == null)
                return null;
            ColonistStatusComponent status = colonist.GetComponent<ColonistStatusComponent>();
            return status != null ? status : colonist.gameObject.AddComponent<ColonistStatusComponent>();
        }

        private static float HomeRestfulness(ColonistAgent colonist)
        {
            if (colonist == null || colonist.home == null)
                return 1f;
            HabitationComponent habitation = colonist.home.GetComponent<HabitationComponent>();
            return habitation != null ? habitation.EffectiveRestfulnessMultiplier : 1f;
        }

        private void QueueCommute(LocationAnchor source, LocationAnchor destination, int priority, ColonistAgent colonist)
        {
            if (source == null || destination == null || source == destination || colonist == null || HasActivePassengerContract(colonist))
                return;
            CollectiveCommute group = null;
            for (int i = 0; i < commuteGroups.Count; i++)
            {
                CollectiveCommute candidate = commuteGroups[i];
                if (candidate.source == source && candidate.destination == destination && candidate.priority == priority)
                {
                    group = candidate;
                    break;
                }
            }
            if (group == null)
            {
                group = new CollectiveCommute { source = source, destination = destination,
                    priority = TransportPriorityRules.Clamp(priority) };
                commuteGroups.Add(group);
            }
            if (!group.colonists.Contains(colonist))
                group.colonists.Add(colonist);
        }

        private void FlushCommutes()
        {
            if (commuteGroups.Count == 0 || ContractManager.Instance == null)
                return;
            int chunkSize = CommuteChunkSize();
            for (int i = 0; i < commuteGroups.Count; i++)
            {
                CollectiveCommute group = commuteGroups[i];
                group.colonists.RemoveAll(HasActivePassengerContract);
                for (int start = 0; start < group.colonists.Count; start += chunkSize)
                {
                    int count = Mathf.Min(chunkSize, group.colonists.Count - start);
                    TransportContract contract = ContractManager.Instance.CreatePassengerContract(
                        group.source, group.destination, group.colonists.GetRange(start, count), group.priority);
                    if (contract == null)
                        Log($"StaffingManager: commute {group.source.displayName} → {group.destination.displayName} could not be created");
                }
            }
        }

        private static int CommuteChunkSize()
        {
            int best = 0;
            if (LogisticsManager.Instance != null)
                for (int i = 0; i < LogisticsManager.Instance.TransportVehicles.Count; i++)
                {
                    TransportVehicleComponent vehicle = LogisticsManager.Instance.TransportVehicles[i];
                    if (vehicle != null && vehicle.personnelEnabled)
                        best = Mathf.Max(best, vehicle.PassengerCapacity);
                }
            return best > 0 ? best : 4;
        }

        public bool IsSafelyAtHome(ColonistAgent colonist)
        {
            return colonist != null && colonist.home != null && colonist.currentLocation == colonist.home &&
                !HasActivePassengerContract(colonist) && colonist.activity != ColonistActivity.Working;
        }

        private static bool HasActivePassengerContract(ColonistAgent colonist)
        {
            return ContractManager.Instance != null && ContractManager.Instance.HasActivePassengerFor(colonist);
        }

        private static bool HasPassengerInTransit(ColonistAgent colonist)
        {
            return colonist != null && colonist.activity == ColonistActivity.Passenger &&
                HasActivePassengerContract(colonist);
        }

        private int CountAssigned(StaffingComponent workplace, StaffingRoleDefinition role, string shiftId, ColonistAgent exclude)
        {
            int count = 0;
            for (int i = 0; i < knownColonists.Count; i++)
            {
                ColonistAgent colonist = knownColonists[i];
                EmploymentAssignment employment = colonist != null && colonist.isActiveAndEnabled && colonist != exclude
                    ? colonist.currentEmployment : null;
                if (employment != null && employment.IsAssigned && employment.workplace == workplace &&
                    employment.role == role && employment.shiftId == shiftId)
                    count++;
            }
            return count;
        }

        private void UpdateCounts()
        {
            employedCount = 0;
            for (int i = 0; i < knownColonists.Count; i++)
                if (knownColonists[i] != null && knownColonists[i].isActiveAndEnabled && knownColonists[i].IsEmployed)
                    employedCount++;
        }

        private static float CurrentGameHour()
        {
            return SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f;
        }

        private static void Log(string message)
        {
            SimulationLog.Log(message);
        }

        private void SetDiagnostic(string message)
        {
            message = message ?? string.Empty;
            if (lastDiagnostic == message)
                return;
            lastDiagnostic = message;
            if (!string.IsNullOrEmpty(message))
                Log($"StaffingManager: {message}");
        }

        private static string AnchorLabel(LocationAnchor anchor)
        {
            if (anchor == null)
                return "<none>";
            return string.IsNullOrEmpty(anchor.displayName) ? anchor.name : anchor.displayName;
        }

        private static string ColonistLabel(ColonistAgent colonist)
        {
            if (colonist == null)
                return "<none>";
            return string.IsNullOrEmpty(colonist.displayName) ? colonist.name : colonist.displayName;
        }

        private static string ShipLabel(ShipComponent ship)
        {
            if (ship == null)
                return "<none>";
            return string.IsNullOrEmpty(ship.displayName) ? ship.name : ship.displayName;
        }

        private static string RoleLabel(StaffingRoleDefinition role)
        {
            if (role == null)
                return "<none>";
            return string.IsNullOrEmpty(role.displayName) ? role.name : role.displayName;
        }

        private static int CompareColonistsForSchedule(ColonistAgent left, ColonistAgent right)
        {
            int name = string.Compare(ColonistLabel(left), ColonistLabel(right), StringComparison.Ordinal);
            return name != 0 ? name : left.GetEntityId().CompareTo(right.GetEntityId());
        }

        private static string FormatWorkWindow(ShiftDefinition shift)
        {
            if (!ValidDailyShift(shift))
                return "INVALID";
            if (shift.durationHours >= SimulationTime.HoursPerDay)
                return "00:00-24:00";

            float end = shift.startHour + shift.durationHours;
            return end <= SimulationTime.HoursPerDay
                ? $"{FormatDailyBoundary(shift.startHour)}-{FormatDailyBoundary(end)}"
                : $"{FormatDailyBoundary(shift.startHour)}-{FormatDailyBoundary(end - SimulationTime.HoursPerDay)} (+1d)";
        }

        private static string FormatOffDutyWindow(ShiftDefinition shift)
        {
            if (!ValidDailyShift(shift))
                return "INVALID";
            if (shift.durationHours >= SimulationTime.HoursPerDay)
                return "none";

            float start = shift.startHour + shift.durationHours;
            if (start >= SimulationTime.HoursPerDay)
                start -= SimulationTime.HoursPerDay;
            float duration = SimulationTime.HoursPerDay - shift.durationHours;
            float end = start + duration;
            return end <= SimulationTime.HoursPerDay
                ? $"{FormatDailyBoundary(start)}-{FormatDailyBoundary(end)}"
                : $"{FormatDailyBoundary(start)}-{FormatDailyBoundary(end - SimulationTime.HoursPerDay)} (+1d)";
        }

        private static bool ValidDailyShift(ShiftDefinition shift)
        {
            return shift != null &&
                !float.IsNaN(shift.startHour) && !float.IsInfinity(shift.startHour) &&
                !float.IsNaN(shift.durationHours) && !float.IsInfinity(shift.durationHours) &&
                shift.startHour >= 0f && shift.startHour < SimulationTime.HoursPerDay &&
                shift.durationHours > 0f && shift.durationHours <= SimulationTime.HoursPerDay;
        }

        private static string FormatDailyBoundary(float hour)
        {
            if (Mathf.Abs(hour - SimulationTime.HoursPerDay) < 0.0001f)
                return "24:00";
            return SimulationTime.FormatHourOfDay(hour);
        }

        private static string FormatLastDuty(ColonistStatusComponent status)
        {
            if (status == null)
                return "-";
            DutyRecord duty = status.ActiveDuty;
            bool active = duty != null;
            if (duty == null && status.DutyHistory != null && status.DutyHistory.Count > 0)
                duty = status.DutyHistory[status.DutyHistory.Count - 1];
            if (duty == null)
                return "-";

            string end = active ? "active" : SimulationTime.FormatTimestamp(duty.endGameHour);
            string fatigueEnd = duty.fatigueAtEnd >= 0f ? duty.fatigueAtEnd.ToString("0.00") : "-";
            string reason = active ? "active" : duty.endReason.ToString();
            return $"{SimulationTime.FormatTimestamp(duty.startGameHour)}-{end} ({duty.workedGameHours:0.00}h, fatigue {duty.fatigueAtStart:0.00}->{fatigueEnd}, {reason})";
        }

        private class CollectiveCommute
        {
            public LocationAnchor source;
            public LocationAnchor destination;
            public int priority;
            public readonly List<ColonistAgent> colonists = new List<ColonistAgent>();
        }
    }
}
