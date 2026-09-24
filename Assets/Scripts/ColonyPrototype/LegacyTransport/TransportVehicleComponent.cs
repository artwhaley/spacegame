using UnityEngine;

namespace AsteroidColony
{
    public enum TransportDisposition
    {
        Neutral,
        FreightOnly,
        PersonnelOnly,
        PreferFreight,
        PreferPersonnel
    }

    public enum VehicleAvailabilityReason
    {
        Available,
        MissingShip,
        ComponentDisabled,
        ShipDisabled,
        OperationalDisabled,
        MovementNotDocked,
        MissingCrewBase,
        CrewUnavailable,
        NoQualifiedPilot,
        PilotNotAboard,
        Busy,
        ReleaseRequested,
        FreightDisabled,
        PersonnelDisabled
    }

    public struct VehicleAvailability
    {
        public bool Available;
        public VehicleAvailabilityReason Reason;
        public string Blocker;

        public VehicleAvailability(bool available, VehicleAvailabilityReason reason, string blocker)
        {
            Available = available;
            Reason = reason;
            Blocker = blocker;
        }
    }

    /// <summary>Declares a ship's participation and capabilities in logistics.</summary>
    public class TransportVehicleComponent : MonoBehaviour
    {
        public ShipComponent ship;
        public InventoryComponent cargoInventory;
        public PassengerCarrierComponent passengerCarrier;
        public TransportDisposition disposition = TransportDisposition.Neutral;
        public bool freightEnabled = true;
        public bool personnelEnabled = true;

        private TransportExecutorComponent executor;

        public string DisplayName => ship != null && !string.IsNullOrEmpty(ship.displayName)
            ? ship.displayName
            : name;

        private TransportExecutorComponent Executor => executor != null
            ? executor
            : executor = GetComponent<TransportExecutorComponent>();

        public bool HasActiveWork => Executor != null && Executor.CurrentContract != null &&
            Executor.CurrentContract.IsAssignedOrInFlight;

        public bool IsAvailable
        {
            get
            {
                TryGetAvailability(out VehicleAvailability availability);
                return availability.Available;
            }
        }

        public int PassengerCapacity => passengerCarrier != null ? passengerCarrier.passengerCapacity : 0;

        private void Awake()
        {
            if (ship == null)
                ship = GetComponent<ShipComponent>();
            if (cargoInventory == null)
                cargoInventory = GetComponent<InventoryComponent>();
            if (passengerCarrier == null)
                passengerCarrier = GetComponent<PassengerCarrierComponent>();
            executor = GetComponent<TransportExecutorComponent>();
        }

        private void OnEnable()
        {
            RegisterWithLogistics();
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private void OnDisable()
        {
            // Retain the registry entry so dispatch/UI can report a disabled
            // vehicle instead of treating it as nonexistent.
        }

        private void OnDestroy()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.UnregisterTransportVehicle(this);
        }

        private void RegisterWithLogistics()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.RegisterTransportVehicle(this);
        }

        public float GetFreeCargoCapacity(ResourceDefinition resource)
        {
            if (cargoInventory == null || resource == null)
                return 0f;

            float free = Mathf.Max(0f, cargoInventory.GetFreeCapacity(resource));
            return resource.IsDiscrete ? Mathf.Floor(free + ResourceQuantityRules.WholeNumberEpsilon) : free;
        }

        public string AvailabilityBlocker()
        {
            TryGetAvailability(out VehicleAvailability availability);
            return availability.Blocker;
        }

        public bool TryGetAvailability(out VehicleAvailability availability)
        {
            if (ship == null)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.MissingShip, "no ship component");
                return false;
            }
            if (!isActiveAndEnabled)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.ComponentDisabled, "transport component disabled");
                return false;
            }
            if (!ship.isActiveAndEnabled)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.ShipDisabled, "ship component disabled");
                return false;
            }
            if (!ship.operationalEnabled)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.OperationalDisabled, "operational disabled");
                return false;
            }
            if (ship.IsTraveling || !ship.HasSafeDock)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.MovementNotDocked, "ship is not safely docked");
                return false;
            }
            if (ship.crewChangeBase == null)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.MissingCrewBase,
                    "ship is missing an authored crew-change base");
                return false;
            }
            if (ship.crewStaffing == null || !ship.crewStaffing.isActiveAndEnabled ||
                !ship.IsOperationallyCrewed)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.CrewUnavailable, ship.ReadinessBlocker());
                return false;
            }
            if (!ship.HasQualifiedPilot)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.NoQualifiedPilot, "no qualified pilot");
                return false;
            }
            LocationAnchor location = GetComponent<LocationAnchor>();
            if (location != null && ship.ResponsiblePilot != null && ship.ResponsiblePilot.currentLocation != location)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.PilotNotAboard, "pilot not aboard");
                return false;
            }
            if (ship.ReleaseRequested)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.ReleaseRequested,
                    $"pilot release requested ({ship.ReleaseReason})");
                return false;
            }
            if (HasActiveWork)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.Busy,
                    $"busy with contract #{Executor.CurrentContract.contractId}");
                return false;
            }
            availability = new VehicleAvailability(true, VehicleAvailabilityReason.Available, "available");
            return true;
        }

        public void SetDisposition(TransportDisposition next) => disposition = next;
        public void SetFreightEnabled(bool enabled) => freightEnabled = enabled;
        public void SetPersonnelEnabled(bool enabled) => personnelEnabled = enabled;

        public bool StartContract(TransportContract contract)
        {
            if (Executor == null || contract == null || !IsAvailable)
                return false;
            if (contract.type == TransportContractType.Freight && !freightEnabled)
                return false;
            if (contract.type == TransportContractType.Passenger && !personnelEnabled)
                return false;
            Executor.StartContract(contract);
            return Executor.CurrentContract == contract;
        }
    }
}
