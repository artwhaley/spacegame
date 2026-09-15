using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Command Post Food and Water consumption plus automatic resupply demand.
    ///
    /// NOTE (Phase 1 simplification): all Command Post residents consume food from
    /// the Command Post inventory regardless of their current physical location.
    /// This is temporary and must not be mistaken for the intended final design.
    /// </summary>
    public class CommandPostController : MonoBehaviour, ISimulationTickable
    {
        public LocationAnchor commandPostLocation;
        public InventoryComponent commandPostInventory;
        public LocationAnchor foodSourceFarm;
        public float consumptionPerResidentPerHour = 0.1f;
        public float reorderThreshold = 8f;
        public float targetFoodStock = 20f;
        public float maximumOrderSize = 12f;
        public float waterConsumptionPerResidentPerHour = 0.1f;
        public float waterReorderThreshold = 8f;
        public float waterTargetStock = 16f;
        public float waterMinimumShipment = 4f;
        public float waterMaximumOrderSize = 12f;
        public int waterResupplyPriority = 90;

        [SerializeField] private bool foodShortageActive;
        [SerializeField] private bool waterShortageActive;
        [SerializeField] private int residents;
        [SerializeField] private float currentConsumptionRate;
        [SerializeField] private float currentWaterConsumptionRate;
        [SerializeField] private float waterOnHand;
        [SerializeField] private float waterIncoming;

        public bool FoodShortageActive => foodShortageActive;
        public bool WaterShortageActive => waterShortageActive;
        public float WaterOnHand => waterOnHand;
        public float WaterIncoming => waterIncoming;
        public float CurrentWaterConsumptionRate => currentWaterConsumptionRate;

        private InventoryComponent foodSourceInventory;
        private int waterDemandId;
        private bool started;

        private void Awake()
        {
            if (commandPostLocation == null)
                commandPostLocation = GetComponent<LocationAnchor>();
            if (commandPostInventory == null)
                commandPostInventory = GetComponent<InventoryComponent>();
        }

        private void Start()
        {
            started = true;
            if (foodSourceFarm != null)
                foodSourceInventory = foodSourceFarm.GetComponent<InventoryComponent>();
            RegisterWaterDemand();
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        private void OnEnable()
        {
            if (started)
                RegisterWaterDemand();
        }

        private void RegisterWaterDemand()
        {
            if (LogisticsManager.Instance != null)
            {
                waterDemandId = LogisticsManager.Instance.RegisterFreightDemand(
                    "Command Post Water",
                    ResourceType.Water,
                    commandPostLocation,
                    commandPostInventory,
                    waterResupplyPriority,
                    waterMinimumShipment,
                    waterMaximumOrderSize);
            }
        }

        private void OnDisable()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.UnregisterFreightDemand(waterDemandId);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            TickFoodConsumption(deltaGameHours);
            TickWaterConsumption(deltaGameHours);
            PublishWaterDemand();
            TickFoodResupplyDemand();
            RefreshWaterObservability();
        }

        private void TickFoodConsumption(float deltaGameHours)
        {
            // Phase 1 simplification: ALL residents count even when temporarily away.
            residents = PopulationManager.Instance != null
                ? PopulationManager.Instance.TotalPopulation
                : 0;
            currentConsumptionRate = residents * consumptionPerResidentPerHour;

            float requested = currentConsumptionRate * deltaGameHours;
            if (requested <= 0f)
                return;

            float removed = commandPostInventory != null
                ? commandPostInventory.Remove(ResourceType.Food, requested)
                : 0f;

            bool shortageNow = removed < requested - 0.0001f;
            if (shortageNow && !foodShortageActive)
            {
                foodShortageActive = true;
                SimulationLog.Log("Food shortage at Command Post - inventory empty");
            }
            else if (!shortageNow && foodShortageActive)
            {
                foodShortageActive = false;
                SimulationLog.Log("Food shortage at Command Post resolved");
            }
        }

        private void TickWaterConsumption(float deltaGameHours)
        {
            residents = PopulationManager.Instance != null
                ? PopulationManager.Instance.TotalPopulation
                : 0;
            currentWaterConsumptionRate = residents * waterConsumptionPerResidentPerHour;

            float requested = currentWaterConsumptionRate * deltaGameHours;
            if (requested <= 0f)
                return;

            float removed = commandPostInventory != null
                ? commandPostInventory.Remove(ResourceType.Water, requested)
                : 0f;
            bool shortageNow = removed < requested - 0.0001f;
            if (shortageNow && !waterShortageActive)
            {
                waterShortageActive = true;
                SimulationLog.Log("Command Post Water shortage began");
            }
            else if (!shortageNow && waterShortageActive)
            {
                waterShortageActive = false;
                SimulationLog.Log("Command Post Water shortage ended");
            }
        }

        private void TickFoodResupplyDemand()
        {
            if (commandPostInventory == null || foodSourceInventory == null ||
                ContractManager.Instance == null)
            {
                return;
            }

            float onHand = commandPostInventory.GetOnHand(ResourceType.Food);
            if (onHand >= reorderThreshold)
                return;

            // Only one active Command Post food-resupply contract at a time.
            if (ContractManager.Instance.HasActiveFreightTo(commandPostLocation, ResourceType.Food))
                return;

            float desired = Mathf.Min(targetFoodStock - onHand, maximumOrderSize);
            float farmAvailable = foodSourceInventory.GetAvailable(ResourceType.Food);
            float quantity = Mathf.Min(desired, farmAvailable);
            if (quantity <= 0f)
                return;

            ContractManager.Instance.CreateFreightContract(
                ResourceType.Food, quantity,
                foodSourceInventory, commandPostInventory,
                foodSourceFarm, commandPostLocation);
        }

        private void PublishWaterDemand()
        {
            if (waterDemandId <= 0 || commandPostInventory == null ||
                commandPostLocation == null || LogisticsManager.Instance == null)
            {
                return;
            }

            float onHand = commandPostInventory.GetOnHand(ResourceType.Water);
            float desired = onHand < waterReorderThreshold
                ? Mathf.Max(0f, waterTargetStock - onHand)
                : 0f;
            LogisticsManager.Instance.UpdateFreightDemand(waterDemandId, desired);
        }

        private void RefreshWaterObservability()
        {
            waterOnHand = commandPostInventory != null
                ? commandPostInventory.GetOnHand(ResourceType.Water)
                : 0f;
            waterIncoming = ContractManager.Instance != null && waterDemandId > 0
                ? ContractManager.Instance.GetActiveFreightQuantityForDemand(waterDemandId)
                : 0f;
        }
    }
}
