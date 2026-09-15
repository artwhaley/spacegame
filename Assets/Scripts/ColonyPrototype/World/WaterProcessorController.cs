using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Automated local 1:1 Ice-to-Water processing for Phase 2.
    /// </summary>
    public class WaterProcessorController : MonoBehaviour, ISimulationTickable
    {
        public InventoryComponent inventory;
        public ResourceDefinition iceResource;
        public ResourceDefinition waterResource;
        public bool operationalEnabled = true;
        public float iceInputTarget = 24f;
        public float iceConsumptionRate = 2f;
        public float waterProductionRate = 2f;

        [SerializeField] private bool currentlyProcessing;
        [SerializeField] private bool inputStarved;
        [SerializeField] private bool outputBlocked;
        [SerializeField] private float iceOnHand;
        [SerializeField] private float iceAvailable;
        [SerializeField] private float iceCapacity;
        [SerializeField] private float waterOnHand;
        [SerializeField] private float waterReserved;
        [SerializeField] private float waterAvailable;
        [SerializeField] private float waterCapacity;

        private bool wasProcessing;
        private bool hasReportedState;
        private int waterSupplyId;
        private bool started;

        private const float QuantityEpsilon = 0.0001f;

        public bool CurrentlyProcessing => currentlyProcessing;
        public bool InputStarved => inputStarved;
        public bool OutputBlocked => outputBlocked;
        public float IceOnHand => inventory != null ? inventory.GetOnHand(iceResource) : 0f;
        public float IceAvailable => inventory != null ? inventory.GetAvailable(iceResource) : 0f;
        public float IceStorageFreeCapacity => inventory != null ? inventory.GetFreeCapacity(iceResource) : 0f;
        public float WaterOnHand => inventory != null ? inventory.GetOnHand(waterResource) : 0f;
        public float WaterReserved => inventory != null ? inventory.GetReserved(waterResource) : 0f;
        public float WaterAvailable => inventory != null ? inventory.GetAvailable(waterResource) : 0f;
        public float WaterCapacity => inventory != null ? inventory.GetCapacity(waterResource) : 0f;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
            RefreshObservability();
        }

        private void Start()
        {
            started = true;
            RegisterWaterSupply();
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        private void OnEnable()
        {
            if (started)
                RegisterWaterSupply();
        }

        private void OnDisable()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.UnregisterFreightSupply(waterSupplyId);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        private void RegisterWaterSupply()
        {
            if (LogisticsManager.Instance != null && inventory != null)
            {
                waterSupplyId = LogisticsManager.Instance.RegisterFreightSupply(
                    "Water Processor",
                    waterResource,
                    GetComponent<LocationAnchor>(),
                    inventory);
            }
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (inventory == null)
                return;

            float ice = IceOnHand;
            float waterFree = inventory.GetFreeCapacity(waterResource);
            float ratio = waterProductionRate > 0f && iceConsumptionRate > 0f
                ? waterProductionRate / iceConsumptionRate
                : 0f;

            inputStarved = ice <= QuantityEpsilon;
            outputBlocked = !inputStarved && (waterFree <= QuantityEpsilon || ratio <= 0f);
            currentlyProcessing = operationalEnabled && !inputStarved && !outputBlocked &&
                                  iceConsumptionRate > 0f && waterProductionRate > 0f;

            if (currentlyProcessing)
            {
                float maxByOutput = waterFree / ratio;
                float amount = Mathf.Min(iceConsumptionRate * deltaGameHours,
                    Mathf.Min(ice, maxByOutput));
                float consumed = inventory.Remove(iceResource, amount);
                float produced = inventory.Add(waterResource, consumed * ratio);

                // The amount was capped by free output capacity. This guard keeps
                // conservation exact if a future caller changes inventory behavior.
                if (produced < consumed * ratio - QuantityEpsilon)
                    inventory.Add(iceResource, (consumed * ratio - produced) / ratio);

                currentlyProcessing = consumed > QuantityEpsilon && produced > QuantityEpsilon;
            }

            if (currentlyProcessing && !wasProcessing)
                SimulationLog.Log(hasReportedState
                    ? "Water Processor resumed processing"
                    : "Water Processor started processing");
            else if (!currentlyProcessing && wasProcessing)
                LogStoppedState();
            else if (!hasReportedState && !currentlyProcessing)
                LogStoppedState();

            wasProcessing = currentlyProcessing;
            hasReportedState = true;
            RefreshObservability();
        }

        private void LogStoppedState()
        {
            if (inputStarved)
                SimulationLog.Log("Water Processor stopped: no Ice");
            else if (outputBlocked)
                SimulationLog.Log("Water Processor stopped: Water storage full");
            else if (!operationalEnabled)
                SimulationLog.Log("Water Processor stopped: disabled");
        }

        private void RefreshObservability()
        {
            iceOnHand = inventory != null ? inventory.GetOnHand(iceResource) : 0f;
            iceAvailable = inventory != null ? inventory.GetAvailable(iceResource) : 0f;
            iceCapacity = inventory != null ? inventory.GetCapacity(iceResource) : 0f;
            waterOnHand = inventory != null ? inventory.GetOnHand(waterResource) : 0f;
            waterReserved = inventory != null ? inventory.GetReserved(waterResource) : 0f;
            waterAvailable = inventory != null ? inventory.GetAvailable(waterResource) : 0f;
            waterCapacity = inventory != null ? inventory.GetCapacity(waterResource) : 0f;
        }
    }
}
