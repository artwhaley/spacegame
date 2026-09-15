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

        public bool HasActiveWork => executor != null && executor.CurrentContract != null &&
            executor.CurrentContract.IsActive;

        public bool IsAvailable => ship != null && ship.IsOperationallyCrewed && !HasActiveWork;

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
            if (!ship.operationalEnabled)
                return "operational disabled";
            if (!ship.HasQualifiedPilot)
                return "no qualified pilot";
            LocationAnchor location = GetComponent<LocationAnchor>();
            if (location != null && ship.assignedPilot.currentLocation != location)
                return "pilot not aboard";
            if (HasActiveWork)
                return $"busy with contract #{executor.CurrentContract.contractId}";
            return "available";
        }
    }
}
