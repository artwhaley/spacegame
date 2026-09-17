using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Progressive worker states used when querying a role.</summary>
    public enum StaffingWorkerStage
    {
        Assigned,
        Present,
        Working,
        Active
    }

    /// <summary>Serializable runtime snapshot so the Inspector can show who is where.</summary>
    [Serializable]
    public class StaffingDebugRow
    {
        public string role;
        public string shift;
        public int assigned;
        public int present;
        public int working;
        public int activeQualified;
        public int minimumActive;
        public int maximumAssigned;
    }

    /// <summary>
    /// Attached to a workplace. It declares the staffing roles the facility offers
    /// and publishes facility performance from the number of active qualified
    /// workers per role. It never allocates jobs, schedules shifts, or owns
    /// commuting (StaffingManager does) and stores no ordered worker slots.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaffingComponent : MonoBehaviour, IFacilityPerformanceProvider, ISimulationTickable, ISimulationTickPriority
    {
        public LocationAnchor workplaceLocation;
        public ShiftPatternDefinition shiftPattern;
        public List<StaffingRoleDefinition> offeredRoles = new List<StaffingRoleDefinition>();
        [Range(1, 10)] public int commutePriority = 10;

        /// <summary>True when this staffing roster belongs to the ship on this GameObject.</summary>
        public bool IsShipCrewRoster => GetComponent<ShipComponent>() != null;
        public ShipComponent Ship => GetComponent<ShipComponent>();

        [SerializeField] private List<StaffingDebugRow> debugRows = new List<StaffingDebugRow>();

        private readonly List<ColonistAgent> collected = new List<ColonistAgent>();
        private readonly List<ColonistAgent> activeScratch = new List<ColonistAgent>();

        public IReadOnlyList<StaffingDebugRow> DebugRows => debugRows;
        public int SimulationTickPriority => 200;

        public string DisplayName =>
            workplaceLocation != null && !string.IsNullOrEmpty(workplaceLocation.displayName)
                ? workplaceLocation.displayName
                : name;

        private void Awake()
        {
            if (workplaceLocation == null)
                workplaceLocation = GetComponent<LocationAnchor>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            RefreshDebugRows(CurrentGameHour());
        }

        public bool OffersRole(StaffingRoleDefinition role)
        {
            if (role == null || offeredRoles == null)
                return false;
            for (int i = 0; i < offeredRoles.Count; i++)
                if (offeredRoles[i] == role)
                    return true;
            return false;
        }

        public ShiftDefinition GetShift(string shiftId)
        {
            return shiftPattern != null ? shiftPattern.FindShift(shiftId) : null;
        }

        public IReadOnlyList<ColonistAgent> GetAssignedWorkers(StaffingRoleDefinition role, string shiftId)
        {
            return Query(role, shiftId, StaffingWorkerStage.Assigned, CurrentGameHour());
        }

        public IReadOnlyList<ColonistAgent> GetPresentWorkers(StaffingRoleDefinition role, string shiftId)
        {
            return Query(role, shiftId, StaffingWorkerStage.Present, CurrentGameHour());
        }

        public IReadOnlyList<ColonistAgent> GetWorkingWorkers(StaffingRoleDefinition role, string shiftId)
        {
            return Query(role, shiftId, StaffingWorkerStage.Working, CurrentGameHour());
        }

        public IReadOnlyList<ColonistAgent> GetActiveQualifiedWorkers(StaffingRoleDefinition role, string shiftId)
        {
            return Query(role, shiftId, StaffingWorkerStage.Active, CurrentGameHour());
        }

        public IReadOnlyList<ColonistAgent> Query(
            StaffingRoleDefinition role, string shiftId, StaffingWorkerStage stage, float absoluteGameHour)
        {
            List<ColonistAgent> result = new List<ColonistAgent>();
            CollectWorkers(role, shiftId, stage, absoluteGameHour, result);
            return result;
        }

        /// <summary>Fills <paramref name="buffer"/> with workers of the role at the requested stage. Never clears it.</summary>
        public void CollectWorkers(
            StaffingRoleDefinition role, string shiftId, StaffingWorkerStage stage, float absoluteGameHour,
            List<ColonistAgent> buffer)
        {
            if (buffer == null)
                return;

            collected.Clear();
            if (role != null && StaffingManager.Instance != null)
                StaffingManager.Instance.CollectAssignedWorkers(this, role, shiftId, collected);

            for (int i = 0; i < collected.Count; i++)
            {
                ColonistAgent worker = collected[i];
                if (worker != null && MeetsStage(worker, role, stage, absoluteGameHour))
                    buffer.Add(worker);
            }
        }

        public void PublishPerformance(FacilityPerformanceSnapshot snapshot, float absoluteGameHour)
        {
            if (snapshot == null || offeredRoles == null)
                return;

            for (int i = 0; i < offeredRoles.Count; i++)
            {
                StaffingRoleDefinition role = offeredRoles[i];
                if (role == null)
                    continue;
                if (role.requiredClass == null)
                {
                    snapshot.AddBlocker($"{RoleLabel(role)} has no required class");
                    continue;
                }

                activeScratch.Clear();
                CollectWorkers(role, null, StaffingWorkerStage.Active, absoluteGameHour, activeScratch);
                int activeCount = activeScratch.Count;

                if (activeCount < role.minimumActiveForOperation)
                    snapshot.AddBlocker($"{RoleLabel(role)} {activeCount}/{role.minimumActiveForOperation} active");

                if (role.effects == null)
                    continue;
                for (int e = 0; e < role.effects.Count; e++)
                {
                    StaffingEffectRule rule = role.effects[e];
                    if (rule != null)
                        snapshot.Multiply(rule.effect, rule.Evaluate(activeScratch));
                }
            }
        }

        /// <summary>Recomputes the serialized Inspector snapshot. Purely observational.</summary>
        public void RefreshDebugRows(float absoluteGameHour)
        {
            if (debugRows == null)
                debugRows = new List<StaffingDebugRow>();
            debugRows.Clear();
            if (offeredRoles == null)
                return;

            for (int i = 0; i < offeredRoles.Count; i++)
            {
                StaffingRoleDefinition role = offeredRoles[i];
                if (role == null)
                    continue;

                string roleName = role.displayName;
                if (shiftPattern == null || shiftPattern.shifts == null || shiftPattern.shifts.Count == 0)
                {
                    debugRows.Add(BuildDebugRow(role, roleName, "<none>", absoluteGameHour));
                    continue;
                }

                for (int s = 0; s < shiftPattern.shifts.Count; s++)
                {
                    ShiftDefinition shift = shiftPattern.shifts[s];
                    string shiftName = shift != null ? shift.shiftId : "<null>";
                    debugRows.Add(BuildDebugRow(role, roleName, shiftName, absoluteGameHour));
                }
            }
        }

        private StaffingDebugRow BuildDebugRow(
            StaffingRoleDefinition role, string roleName, string shiftName, float absoluteGameHour)
        {
            return new StaffingDebugRow
            {
                role = roleName,
                shift = shiftName,
                assigned = Count(role, shiftName, StaffingWorkerStage.Assigned, absoluteGameHour),
                present = Count(role, shiftName, StaffingWorkerStage.Present, absoluteGameHour),
                working = Count(role, shiftName, StaffingWorkerStage.Working, absoluteGameHour),
                activeQualified = Count(role, shiftName, StaffingWorkerStage.Active, absoluteGameHour),
                minimumActive = role.minimumActiveForOperation,
                maximumAssigned = role.maximumAssignedPerShift
            };
        }

        private int Count(StaffingRoleDefinition role, string shiftId, StaffingWorkerStage stage, float absoluteGameHour)
        {
            collected.Clear();
            if (role != null && StaffingManager.Instance != null)
                StaffingManager.Instance.CollectAssignedWorkers(this, role, shiftId, collected);
            int count = 0;
            for (int i = 0; i < collected.Count; i++)
                if (collected[i] != null && MeetsStage(collected[i], role, stage, absoluteGameHour))
                    count++;
            return count;
        }

        private bool MeetsStage(
            ColonistAgent worker, StaffingRoleDefinition role, StaffingWorkerStage stage, float absoluteGameHour)
        {
            if (worker == null || role == null)
                return false;
            if (stage == StaffingWorkerStage.Assigned)
                return true;
            if (worker.currentLocation != workplaceLocation)
                return false;
            if (stage == StaffingWorkerStage.Present)
                return true;
            ShipComponent ship = GetComponent<ShipComponent>();
            ColonistStatusComponent status = worker.GetComponent<ColonistStatusComponent>();
            DutyRecord duty = status != null ? status.ActiveDuty : null;
            bool shipDuty = ship != null && worker == ship.ResponsiblePilot &&
                ship.isActiveAndEnabled && ship.operationalEnabled && !ship.ReleaseRequested &&
                worker.activity == ColonistActivity.OnDutyCrew && duty != null &&
                duty.isPilotDuty && duty.ship == ship && !duty.releaseRequested;
            if (ship == null && worker.activity != ColonistActivity.Working)
                return false;
            if (ship != null && !shipDuty)
                return false;
            if (status != null && status.IsExhausted)
                return false;
            if (shiftPattern == null)
                return false;

            EmploymentAssignment employment = worker.currentEmployment;
            if (employment == null || employment.workplace != this)
                return false;
            if (!shiftPattern.IsShiftActive(employment.shiftId, absoluteGameHour))
                return false;
            if (stage == StaffingWorkerStage.Working)
                return true;
            return worker.HasClass(role.requiredClass);
        }

        private static string RoleLabel(StaffingRoleDefinition role)
        {
            return string.IsNullOrEmpty(role.displayName) ? role.name : role.displayName;
        }

        private static float CurrentGameHour()
        {
            return SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f;
        }
    }
}
