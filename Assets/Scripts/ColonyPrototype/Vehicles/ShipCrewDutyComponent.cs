using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum ShipCrewDutyState
    {
        Docked,
        ReturningHome,
        Holding
    }

    /// <summary>
    /// Owns the temporary physical pilot lease. Persistent crew assignments are
    /// queried from the ship's StaffingComponent, so an off-shift pilot never
    /// reserves the shuttle.
    /// </summary>
    public class ShipCrewDutyComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public ShipComponent ship;
        public ShipCrewDutyState state = ShipCrewDutyState.Docked;

        [SerializeField] private LocationAnchor returnDestination;
        [SerializeField] private string lastDiagnostic = string.Empty;

        private ShipMovementComponent movement;
        private readonly List<ColonistAgent> eligibleScratch = new List<ColonistAgent>();

        public int SimulationTickPriority => 350;
        public ShipCrewDutyState State => state;
        public LocationAnchor ReturnDestination => returnDestination;
        public ColonistAgent ResponsiblePilot => ship != null ? ship.ResponsiblePilot : null;
        public string LastDiagnostic => lastDiagnostic;

        private void Awake()
        {
            if (ship == null)
                ship = GetComponent<ShipComponent>();
            movement = GetComponent<ShipMovementComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void RequestRelease(DutyEndReason reason)
        {
            if (ship == null)
                return;
            ship.RequestRelease(reason);
            returnDestination = ship.crewChangeBase;
            state = ShipCrewDutyState.ReturningHome;
            SetDiagnostic(string.Empty);
        }

        public bool TryBoardEligiblePilot()
        {
            if (ship == null || !isActiveAndEnabled || !ship.isActiveAndEnabled || !ship.operationalEnabled ||
                ship.crewStaffing == null || !ship.crewStaffing.isActiveAndEnabled ||
                ship.crewStaffing.shiftPattern == null || ship.ReleaseRequested ||
                ship.ResponsiblePilot != null || ship.CurrentDock != ship.crewChangeBase ||
                ship.IsTraveling || ship.MovementOwner != ShipMovementOwner.None ||
                ship.operatingRole == null ||
                ship.crewChangeBase == null || StaffingManager.Instance == null)
                return false;

            eligibleScratch.Clear();
            StaffingManager.Instance.CollectAssignedWorkers(
                ship.crewStaffing, ship.operatingRole, null, eligibleScratch);
            float hour = SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f;
            ColonistAgent selected = null;
            for (int i = 0; i < eligibleScratch.Count; i++)
            {
                ColonistAgent pilot = eligibleScratch[i];
                EmploymentAssignment employment = pilot != null ? pilot.currentEmployment : null;
                ColonistStatusComponent status = pilot != null ? pilot.GetComponent<ColonistStatusComponent>() : null;
                bool inPassengerTrip = pilot != null &&
                    ((ContractManager.Instance != null && ContractManager.Instance.HasActivePassengerFor(pilot)) ||
                     pilot.activity == ColonistActivity.Passenger);
                if (pilot == null || employment == null || employment.role != ship.operatingRole ||
                    !ship.crewStaffing.shiftPattern.IsShiftActive(employment.shiftId, hour) ||
                    pilot.home != ship.crewChangeBase || pilot.currentLocation != ship.crewChangeBase ||
                    inPassengerTrip || (status != null && status.IsExhausted) ||
                    HasActiveDutyOnOtherShip(pilot, ship))
                    continue;
                if (selected != null)
                {
                    SetDiagnostic("ambiguous active pilot coverage");
                    state = ShipCrewDutyState.Holding;
                    return false;
                }
                selected = pilot;
            }

            if (selected == null)
            {
                SetDiagnostic("no eligible on-shift pilot at crew-change base");
                state = ShipCrewDutyState.Holding;
                return false;
            }

            if (!ship.TryBoardResponsiblePilot(selected))
                return false;
            ColonistStatusComponent selectedStatus = selected.GetComponent<ColonistStatusComponent>();
            if (selectedStatus != null)
                selectedStatus.BeginPilotDuty(ship, ship.operatingRole, selected.currentEmployment.shiftId, hour);
            selected.activity = ColonistActivity.OnDutyCrew;
            SetDiagnostic(string.Empty);
            state = ShipCrewDutyState.Docked;
            return true;
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (ship == null || !isActiveAndEnabled || !ship.isActiveAndEnabled)
                return;

            // A release request is the handoff boundary. Once the ship has been
            // taken out of service (or its roster is disabled), the responsible
            // pilot must still be allowed to finish the safe return; otherwise
            // the early operational guard strands the pilot aboard forever.
            bool hasReturningPilot = ship.ReleaseRequested && ship.ResponsiblePilot != null;
            if ((!ship.operationalEnabled || ship.crewStaffing == null ||
                !ship.crewStaffing.isActiveAndEnabled) && !hasReturningPilot)
                return;

            if (!ship.ReleaseRequested && ship.ResponsiblePilot == null)
            {
                if (ship.CurrentDock == ship.crewChangeBase)
                    TryBoardEligiblePilot();
                return;
            }

            if (!ship.ReleaseRequested || ship.ResponsiblePilot == null)
                return;

            returnDestination = ship.crewChangeBase;
            if (returnDestination == null)
            {
                state = ShipCrewDutyState.Holding;
                SetDiagnostic("missing crew-change base");
                return;
            }
            if (HasActiveShipOperation())
            {
                state = ShipCrewDutyState.ReturningHome;
                return;
            }
            state = ShipCrewDutyState.ReturningHome;

            if (ship.CurrentDock == returnDestination)
            {
                ReleaseAtBase();
                return;
            }
            if (movement == null || !movement.isActiveAndEnabled)
            {
                state = ShipCrewDutyState.Holding;
                SetDiagnostic("missing ship movement component");
                return;
            }
            // A disabled/destroyed operation component can leave its movement
            // lease serialized on the ship even though no operation remains.
            // Once HasActiveShipOperation() is false, that lease is stale and
            // must not prevent the crew-return handoff.
            if (ship.MovementOwner != ShipMovementOwner.None)
                ship.ReleaseMovement(ship.MovementOwner);
            if (!ship.TryClaimMovement(ShipMovementOwner.CrewReturn))
            {
                state = ShipCrewDutyState.Holding;
                SetDiagnostic($"movement owned by {ship.MovementOwner}");
                return;
            }
            if (!ship.IsTraveling)
                ship.MarkDeparted();
            if (movement.MoveToward(returnDestination, Mathf.Max(0f, deltaGameHours)))
            {
                ship.BeginDocking(returnDestination);
                if (ship.TryCompleteArrival(ShipMovementOwner.CrewReturn, returnDestination))
                {
                    ship.ReleaseMovement(ShipMovementOwner.CrewReturn);
                    ReleaseAtBase();
                }
            }
        }

        private void ReleaseAtBase()
        {
            ColonistAgent pilot = ship.ResponsiblePilot;
            DutyEndReason reason = ship.ReleaseReason;
            ColonistStatusComponent status = pilot != null ? pilot.GetComponent<ColonistStatusComponent>() : null;
            ship.ReleaseResponsiblePilotToDock();
            if (status != null && status.HasActiveDuty)
                status.EndDuty(CurrentHour(), reason);
            if (pilot != null)
                pilot.activity = ColonistActivity.Idle;
            state = ShipCrewDutyState.Docked;
            SetDiagnostic(string.Empty);
            SimulationLog.Log($"{ship.displayName} returned pilot to {returnDestination.displayName}");
        }

        private void SetDiagnostic(string message)
        {
            message = message ?? string.Empty;
            if (lastDiagnostic == message)
                return;
            lastDiagnostic = message;
            if (!string.IsNullOrEmpty(message))
                SimulationLog.Log($"{ship?.displayName ?? name} crew blocker: {message}");
        }

        private bool HasActiveShipOperation()
        {
            TransportExecutorComponent executor = GetComponent<TransportExecutorComponent>();
            if (executor != null && executor.isActiveAndEnabled && executor.CurrentContract != null &&
                executor.CurrentContract.IsAssignedOrInFlight &&
                (executor.CurrentContract.assignedVehicle == null ||
                 executor.CurrentContract.assignedVehicle == GetComponent<TransportVehicleComponent>()))
                return true;
            ExtractionMissionController extraction = GetComponent<ExtractionMissionController>();
            return extraction != null && extraction.isActiveAndEnabled && extraction.State != ExtractionMissionState.Idle;
        }

        private static bool HasActiveDutyOnOtherShip(ColonistAgent pilot, ShipComponent currentShip)
        {
            ColonistStatusComponent status = pilot != null ? pilot.GetComponent<ColonistStatusComponent>() : null;
            return status != null && status.HasActiveDuty && status.ActiveDuty.isPilotDuty &&
                status.ActiveDuty.ship != null && status.ActiveDuty.ship != currentShip;
        }

        private static float CurrentHour()
        {
            return SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f;
        }
    }
}
