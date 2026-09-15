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
        public ShuttleController assignedShuttle;
        public float creationTime;
        public int demandId;

        // Freight data
        public ResourceType resourceType;
        public float quantity;
        public float loadedQuantity;
        public float deliveredQuantity;
        public InventoryComponent sourceInventory;
        public InventoryComponent destinationInventory;

        // Passenger data
        public List<ColonistAgent> passengers = new List<ColonistAgent>();

        public bool IsActive =>
            state >= TransportContractState.Open && state <= TransportContractState.Unloading;

        /// <summary>Quantity that has not reached the destination yet.</summary>
        public float RemainingQuantity =>
            quantity > deliveredQuantity ? quantity - deliveredQuantity : 0f;
    }
}
