using System.Collections.Generic;

namespace AsteroidColony
{
    public enum TransportContractType
    {
        Freight,
        Passenger
    }

    public enum TransportContractState
    {
        Open,
        Assigned,
        TravelingToPickup,
        Loading,
        TravelingToDestination,
        Unloading,
        Completed,
        Cancelled
    }

    /// <summary>
    /// Data model for one transport contract, owned by ContractManager.
    /// Freight contracts carry cargo; passenger contracts carry colonists.
    /// </summary>
    [System.Serializable]
    public class TransportContract
    {
        public int contractId;
        public TransportContractType type;
        public int priority;
        public LocationAnchor sourceLocation;
        public LocationAnchor destinationLocation;
        public TransportContractState state;
        public TransportVehicleComponent assignedVehicle;
        public float creationTime;
        public int demandId;

        // Freight data
        public ResourceDefinition resource;
        public float quantity;
        public float loadedQuantity;
        public float deliveredQuantity;
        public InventoryComponent sourceInventory;
        public InventoryComponent destinationInventory;

        // Passenger data
        public List<ColonistAgent> passengers = new List<ColonistAgent>();

        // Contract ids are owned by ContractManager. Unity creates an inline
        // default instance for a null serialized TransportContract field, which
        // has id 0 and must never be treated as live work. Keeping that invariant
        // here prevents every consumer (crew release, dispatch, UI diagnostics)
        // from mistaking serialized/default state for an accepted contract.
        public bool IsActive =>
            contractId > 0 &&
            state >= TransportContractState.Open && state <= TransportContractState.Unloading;

        /// <summary>True only after a vehicle has accepted this contract.</summary>
        public bool IsAssignedOrInFlight =>
            contractId > 0 &&
            state >= TransportContractState.Assigned && state <= TransportContractState.Unloading;

        /// <summary>Quantity that has not reached the destination yet.</summary>
        public float RemainingQuantity =>
            quantity > deliveredQuantity ? quantity - deliveredQuantity : 0f;
    }
}
