using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// A replaceable demand snapshot published by a consumer. Updating this record
    /// changes the current demand; it does not create a transport contract.
    /// </summary>
    [System.Serializable]
    public class FreightDemand
    {
        public int demandId;
        public string displayName;
        public ResourceDefinition resource;
        public LocationAnchor destinationLocation;
        public InventoryComponent destinationInventory;
        public int priority;
        public float minimumShipment;
        public float maximumShipment;
        public bool active;

        [SerializeField] private float desiredQuantity;
        [SerializeField] private float inboundQuantity;
        [SerializeField] private float uncoveredQuantity;
        [SerializeField] private float effectiveDestinationFreeCapacity;
        [SerializeField] private string planningStatus;
        [SerializeField] private long lastUpdatedTick;

        public float DesiredQuantity => desiredQuantity;
        public float InboundQuantity => inboundQuantity;
        public float UncoveredQuantity => uncoveredQuantity;
        public float EffectiveDestinationFreeCapacity => effectiveDestinationFreeCapacity;
        public string PlanningStatus => planningStatus;
        public long LastUpdatedTick => lastUpdatedTick;

        public void Update(float quantity, long tick)
        {
            desiredQuantity = Mathf.Max(0f, quantity);
            active = desiredQuantity > 0.0001f;
            lastUpdatedTick = tick;
        }

        public void SetPlanningState(
            float inbound,
            float uncovered,
            float destinationFree,
            string status)
        {
            inboundQuantity = Mathf.Max(0f, inbound);
            uncoveredQuantity = Mathf.Max(0f, uncovered);
            effectiveDestinationFreeCapacity = Mathf.Max(0f, destinationFree);
            planningStatus = status;
        }
    }

    /// <summary>
    /// A live source of one resource for the freight planner. The inventory remains
    /// the source of truth for available and reserved quantities.
    /// </summary>
    [System.Serializable]
    public class FreightSupply
    {
        public int supplyId;
        public string displayName;
        public ResourceDefinition resource;
        public LocationAnchor location;
        public InventoryComponent inventory;
        public bool active = true;
    }
}
