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

        public bool IsAvailable => isActiveAndEnabled && ship != null && ship.isActiveAndEnabled &&
            ship.IsOperationallyCrewed && ship.HasSafeDock && !ship.IsTraveling &&
            !ship.ReleaseRequested && !HasActiveWork;

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
            if (ship == null)
                return "no ship component";
            if (!isActiveAndEnabled || !ship.isActiveAndEnabled)
                return "transport component disabled";
            if (!ship.operationalEnabled)
                return "operational disabled";
            if (!ship.HasQualifiedPilot)
                return "no qualified pilot";
            LocationAnchor location = GetComponent<LocationAnchor>();
            if (location != null && ship.ResponsiblePilot != null && ship.ResponsiblePilot.currentLocation != location)
                return "pilot not aboard";
            if (!ship.HasSafeDock && !ship.IsTraveling)
                return "no current dock";
            if (HasActiveWork)
                return $"busy with contract #{Executor.CurrentContract.contractId}";
            if (ship.ReleaseRequested)
                return $"pilot release requested ({ship.ReleaseReason})";
            return "available";
        }

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
