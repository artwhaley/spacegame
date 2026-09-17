using UnityEngine;

namespace AsteroidColony
{
    public enum ShipMovementOwner
    {
        None,
        Transport,
        Extraction,
        CrewReturn
    }

    /// <summary>
    /// Shared physical state for a ship. Employment belongs to the sibling
    /// StaffingComponent; only the crew duty component owns the temporary pilot
    /// lease used to operate the ship.
    /// </summary>
    public class ShipComponent : MonoBehaviour
    {
        public string displayName = "Ship";
        public bool operationalEnabled = true;
        public StaffingComponent crewStaffing;
        public StaffingRoleDefinition operatingRole;
        public LocationAnchor crewChangeBase;
        public LocationAnchor initialDock;

        private LocationAnchor shipLocation;
        [SerializeField] private LocationAnchor currentDock;
        [SerializeField] private bool traveling;
        [SerializeField] private bool releaseRequested;
        [SerializeField] private DutyEndReason releaseReason = DutyEndReason.ShiftEnded;
        [SerializeField] private ColonistAgent responsiblePilot;
        [SerializeField] private ShipMovementOwner movementOwner;

        public LocationAnchor ShipLocation => shipLocation;
        public LocationAnchor CurrentDock => currentDock;
        public bool IsTraveling => traveling;
        public bool ReleaseRequested => releaseRequested;
        public DutyEndReason ReleaseReason => releaseReason;
        public ColonistAgent ResponsiblePilot => responsiblePilot;
        public bool IsPilotAboard => responsiblePilot != null && shipLocation != null &&
            responsiblePilot.currentLocation == shipLocation;
        public bool HasQualifiedPilot => responsiblePilot != null && PilotClass != null &&
            responsiblePilot.HasClass(PilotClass);
        public bool IsOperationallyCrewed => isActiveAndEnabled && operationalEnabled && !releaseRequested &&
            shipLocation != null && crewStaffing != null && crewStaffing.isActiveAndEnabled &&
            HasActiveCrewDutyComponent && HasQualifiedPilot && IsPilotAboard && HasActivePilotDuty;
        public bool HasSafeDock => currentDock != null;
        public ShipMovementOwner MovementOwner => movementOwner;
        private WorkerClassDefinition PilotClass => operatingRole != null ? operatingRole.requiredClass : null;
        private bool HasActiveCrewDutyComponent
        {
            get
            {
                ShipCrewDutyComponent crew = GetComponent<ShipCrewDutyComponent>();
                return crew != null && crew.isActiveAndEnabled;
            }
        }
        private bool HasActivePilotDuty
        {
            get
            {
                ColonistStatusComponent status = responsiblePilot != null
                    ? responsiblePilot.GetComponent<ColonistStatusComponent>() : null;
                DutyRecord duty = status != null ? status.ActiveDuty : null;
                return duty != null && duty.isPilotDuty && duty.ship == this &&
                    duty.crewStaffing == crewStaffing && !duty.releaseRequested;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (shipLocation == null)
                shipLocation = GetComponent<LocationAnchor>();
            if (currentDock == null && !traveling)
                currentDock = initialDock;
            if (crewStaffing == null)
                crewStaffing = GetComponent<StaffingComponent>();
            if (crewStaffing != null && crewStaffing.workplaceLocation == null)
                crewStaffing.workplaceLocation = shipLocation;
            if (crewStaffing != null && operatingRole == null && crewStaffing.offeredRoles != null &&
                crewStaffing.offeredRoles.Count > 0)
                operatingRole = crewStaffing.offeredRoles[0];
        }

        private void Start()
        {
            EnsureInitialized();
            if (GetComponent<ShipCrewDutyComponent>() == null)
                gameObject.AddComponent<ShipCrewDutyComponent>();
        }

        public bool TryBoardResponsiblePilot(ColonistAgent pilot)
        {
            if (!isActiveAndEnabled || pilot == null || (responsiblePilot != null && responsiblePilot != pilot) || releaseRequested || shipLocation == null ||
                currentDock == null || pilot.currentLocation != currentDock || traveling)
                return false;
            if (operatingRole == null || operatingRole.requiredClass == null ||
                !pilot.HasClass(operatingRole.requiredClass))
                return false;
            pilot.MoveToLocation(shipLocation);
            responsiblePilot = pilot;
            SimulationLog.Log($"{pilot.displayName} boarded {displayName} at {currentDock.displayName}");
            return true;
        }

        /// <summary>Recovers an already-boarded responsible pilot after manager/component recreation.</summary>
        public bool AdoptResponsiblePilot(ColonistAgent pilot)
        {
            if (pilot == null || responsiblePilot != null || releaseRequested || shipLocation == null ||
                pilot.currentLocation != shipLocation || operatingRole == null || PilotClass == null ||
                !pilot.HasClass(PilotClass))
                return false;
            responsiblePilot = pilot;
            return true;
        }

        public bool TryBoardPilotAtDock()
        {
            ShipCrewDutyComponent crew = GetComponent<ShipCrewDutyComponent>();
            return crew != null && crew.TryBoardEligiblePilot();
        }

        public void ClearResponsiblePilot()
        {
            responsiblePilot = null;
        }

        public void SetDock(LocationAnchor dock)
        {
            currentDock = dock;
            traveling = false;
        }

        public void MarkDeparted()
        {
            currentDock = null;
            traveling = true;
        }

        public bool TryClaimMovement(ShipMovementOwner owner)
        {
            if (owner == ShipMovementOwner.None)
                return false;
            if (movementOwner != ShipMovementOwner.None && movementOwner != owner)
                return false;
            movementOwner = owner;
            return true;
        }

        public void ReleaseMovement(ShipMovementOwner owner)
        {
            if (movementOwner == owner)
                movementOwner = ShipMovementOwner.None;
        }

        public void RequestRelease(DutyEndReason reason)
        {
            if (!releaseRequested)
            {
                releaseRequested = true;
                releaseReason = reason;
                SimulationLog.Log($"{displayName} release requested: {reason}");
            }
        }

        public void ClearReleaseRequest()
        {
            releaseRequested = false;
            releaseReason = DutyEndReason.ShiftEnded;
        }

        public void ReleaseResponsiblePilotToDock()
        {
            if (responsiblePilot == null)
                return;
            if (currentDock != null && shipLocation != null && responsiblePilot.currentLocation == shipLocation)
                responsiblePilot.MoveToLocation(currentDock);
            responsiblePilot.activity = ColonistActivity.Idle;
            responsiblePilot = null;
            ClearReleaseRequest();
        }

        public string ReadinessBlocker()
        {
            if (shipLocation == null)
                return "missing ship anchor";
            if (crewStaffing == null || operatingRole == null)
                return "missing ship crew staffing";
            if (!crewStaffing.isActiveAndEnabled)
                return "crew staffing component disabled";
            if (!HasActiveCrewDutyComponent)
                return "crew duty component disabled or missing";
            if (crewChangeBase == null)
                return "missing crew-change base";
            if (currentDock == null && !traveling)
                return "no current dock";
            if (!operationalEnabled)
                return "operational disabled";
            if (releaseRequested)
                return $"pilot release requested ({releaseReason})";
            if (responsiblePilot == null)
                return "no active pilot";
            if (!HasQualifiedPilot)
                return "active pilot lacks required class";
            if (!IsPilotAboard)
                return "pilot not aboard";
            if (!HasActivePilotDuty)
                return "pilot has no active duty record";
            return "ready";
        }
    }
}
