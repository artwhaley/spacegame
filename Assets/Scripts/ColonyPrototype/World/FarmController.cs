using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum FarmShiftState
    {
        AwaitingWorkTransport,
        Working,
        AwaitingReturnTransport,
        Resting
    }

    /// <summary>
    /// Drives the farmer work/rest cycle, farm food production, and Water demand.
    /// Farmers produce food only while physically present at the farm and in
    /// their working phase. They travel by passenger contract between home and
    /// the farm and only work/rest after physically arriving.
    /// </summary>
    public class FarmController : MonoBehaviour, ISimulationTickable
    {
        public LocationAnchor farmLocation;
        public InventoryComponent farmInventory;
        public ResourceDefinition foodResource;
        public ResourceDefinition waterResource;
        public List<ColonistAgent> assignedFarmers = new List<ColonistAgent>();
        public float workDurationHours = 8f;
        public float restDurationHours = 8f;
        public float foodProductionPerFarmerPerHour = 1f;
        public float waterConsumptionPerFarmerPerHour = 0.5f;
        public float waterReorderThreshold = 3f;
        public float waterTargetStock = 8f;
        public float waterMinimumShipment = 4f;
        public float waterMaximumOrderSize = 8f;
        public int waterResupplyPriority = 60;

        [SerializeField] private FarmShiftState shiftState;
        [SerializeField] private float shiftTimeRemaining;
        [SerializeField] private int presentWorkingFarmers;
        [SerializeField] private float currentProductionRate;
        [SerializeField] private bool productionBlocked;
        [SerializeField] private string productionBlockedReason;
        [SerializeField] private float waterOnHand;
        [SerializeField] private float waterReserved;
        [SerializeField] private float waterIncoming;

        public FarmShiftState ShiftState => shiftState;
        public float ShiftTimeRemaining => shiftTimeRemaining;
        public int PresentWorkingFarmers => presentWorkingFarmers;
        public float CurrentProductionRate => currentProductionRate;
        public bool ProductionBlocked => productionBlocked;
        public string ProductionBlockedReason => productionBlockedReason;
        public float WaterOnHand => waterOnHand;
        public float WaterReserved => waterReserved;
        public float WaterIncoming => waterIncoming;

        private float workHoursAccumulated;
        private float restHoursAccumulated;
        private int waterDemandId;
        private bool started;

        private void Awake()
        {
            if (farmLocation == null)
                farmLocation = GetComponent<LocationAnchor>();
            if (farmInventory == null)
                farmInventory = GetComponent<InventoryComponent>();
        }

        private void Start()
        {
            started = true;
            RegisterWaterDemand();
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);

            // Game start: farmers are at home, rested, and ready to begin a work shift.
            if (assignedFarmers.Count > 0)
                RequestWorkTransport();
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
                    "Farm Water",
                    waterResource,
                    farmLocation,
                    farmInventory,
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
            RefreshWaterObservability();
            PublishWaterDemand();

            switch (shiftState)
            {
                case FarmShiftState.AwaitingWorkTransport:
                    if (AllFarmersAt(farmLocation))
                        BeginWorking();
                    else
                        RequestWorkTransport(); // retries creation if needed
                    break;

                case FarmShiftState.Working:
                    TickWorking(deltaGameHours);
                    break;

                case FarmShiftState.AwaitingReturnTransport:
                    if (AllFarmersAtHome())
                        BeginResting();
                    else
                        RequestReturnTransport(); // retries creation if needed
                    break;

                case FarmShiftState.Resting:
                    TickResting(deltaGameHours);
                    break;
            }
        }

        private void TickWorking(float deltaGameHours)
        {
            presentWorkingFarmers = CountWorkingFarmersAtFarm();
            float requestedWater = presentWorkingFarmers * waterConsumptionPerFarmerPerHour * deltaGameHours;
            float waterUsed = 0f;
            if (requestedWater > 0f && farmInventory != null)
                waterUsed = farmInventory.Remove(waterResource, requestedWater);

            bool blocked = requestedWater > 0f && waterUsed < requestedWater - 0.0001f;
            if (blocked)
            {
                currentProductionRate = waterConsumptionPerFarmerPerHour > 0f
                    ? waterUsed / (waterConsumptionPerFarmerPerHour * deltaGameHours)
                      * foodProductionPerFarmerPerHour
                      * presentWorkingFarmers
                    : 0f;
                SetProductionBlocked(true, "No Water");
            }
            else
            {
                currentProductionRate = presentWorkingFarmers * foodProductionPerFarmerPerHour;
                SetProductionBlocked(false, string.Empty);
            }

            float produced = currentProductionRate * deltaGameHours;
            if (produced > 0f && farmInventory != null)
                farmInventory.Add(foodResource, produced);

            workHoursAccumulated += deltaGameHours;
            shiftTimeRemaining = Mathf.Max(0f, workDurationHours - workHoursAccumulated);

            if (workHoursAccumulated >= workDurationHours)
                EndWorkShift();
        }

        private void TickResting(float deltaGameHours)
        {
            presentWorkingFarmers = 0;
            currentProductionRate = 0f;
            productionBlocked = false;
            productionBlockedReason = string.Empty;

            restHoursAccumulated += deltaGameHours;
            shiftTimeRemaining = Mathf.Max(0f, restDurationHours - restHoursAccumulated);

            if (restHoursAccumulated >= restDurationHours)
                RequestWorkTransport();
        }

        private void BeginWorking()
        {
            for (int i = 0; i < assignedFarmers.Count; i++)
            {
                if (assignedFarmers[i] != null)
                    assignedFarmers[i].activity = ColonistActivity.Working;
            }
            workHoursAccumulated = 0f;
            shiftState = FarmShiftState.Working;
            productionBlocked = false;
            productionBlockedReason = string.Empty;
            SimulationLog.Log($"Farmers arrived at {farmLocation.displayName} and began working");
        }

        private void EndWorkShift()
        {
            for (int i = 0; i < assignedFarmers.Count; i++)
            {
                if (assignedFarmers[i] != null)
                    assignedFarmers[i].activity = ColonistActivity.WaitingForTransport;
            }
            shiftState = FarmShiftState.AwaitingReturnTransport;
            SimulationLog.Log($"Farmers finished their work shift at {farmLocation.displayName}");
            RequestReturnTransport();
        }

        private void BeginResting()
        {
            for (int i = 0; i < assignedFarmers.Count; i++)
            {
                if (assignedFarmers[i] != null)
                    assignedFarmers[i].activity = ColonistActivity.Resting;
            }
            restHoursAccumulated = 0f;
            shiftState = FarmShiftState.Resting;
            SimulationLog.Log("Farmers returned home and began resting");
        }

        private void RequestWorkTransport()
        {
            if (ContractManager.Instance == null)
                return;
            if (ContractManager.Instance.HasActivePassengerForAny(assignedFarmers))
                return;
            if (!AllFarmersAtHome())
                return;

            if (ContractManager.Instance.CreatePassengerContract(GetHome(), farmLocation, assignedFarmers) != null)
            {
                shiftState = FarmShiftState.AwaitingWorkTransport;
                SimulationLog.Log($"Farmers requested transport to {farmLocation.displayName}");
            }
        }

        private void RequestReturnTransport()
        {
            if (ContractManager.Instance == null)
                return;
            if (ContractManager.Instance.HasActivePassengerForAny(assignedFarmers))
                return;
            if (!AllFarmersAt(farmLocation))
                return;

            if (ContractManager.Instance.CreatePassengerContract(farmLocation, GetHome(), assignedFarmers) != null)
                SimulationLog.Log("Farmers requested transport home");
        }

        private bool AllFarmersAtHome()
        {
            return AllFarmersAt(GetHome());
        }

        private bool AllFarmersAt(LocationAnchor location)
        {
            if (location == null)
                return false;
            for (int i = 0; i < assignedFarmers.Count; i++)
            {
                if (assignedFarmers[i] == null || assignedFarmers[i].currentLocation != location)
                    return false;
            }
            return true;
        }

        private LocationAnchor GetHome()
        {
            for (int i = 0; i < assignedFarmers.Count; i++)
                if (assignedFarmers[i] != null && assignedFarmers[i].home != null)
                    return assignedFarmers[i].home;
            return null;
        }

        private int CountWorkingFarmersAtFarm()
        {
            int count = 0;
            for (int i = 0; i < assignedFarmers.Count; i++)
            {
                ColonistAgent farmer = assignedFarmers[i];
                if (farmer != null &&
                    farmer.currentLocation == farmLocation &&
                    farmer.activity == ColonistActivity.Working)
                {
                    count++;
                }
            }
            return count;
        }

        private void RefreshWaterObservability()
        {
            waterOnHand = farmInventory != null ? farmInventory.GetOnHand(waterResource) : 0f;
            waterReserved = farmInventory != null ? farmInventory.GetReserved(waterResource) : 0f;
            waterIncoming = ContractManager.Instance != null && waterDemandId > 0
                ? ContractManager.Instance.GetActiveFreightQuantityForDemand(waterDemandId)
                : 0f;
        }

        private void PublishWaterDemand()
        {
            if (waterDemandId <= 0 || farmInventory == null ||
                farmLocation == null || LogisticsManager.Instance == null)
            {
                return;
            }

            float onHand = farmInventory.GetOnHand(waterResource);
            float desired = onHand < waterReorderThreshold
                ? Mathf.Max(0f, waterTargetStock - onHand)
                : 0f;
            LogisticsManager.Instance.UpdateFreightDemand(waterDemandId, desired);
        }

        private void SetProductionBlocked(bool blocked, string reason)
        {
            if (blocked && !productionBlocked)
                SimulationLog.Log("Farm stopped production: no Water");
            else if (!blocked && productionBlocked)
                SimulationLog.Log("Farm resumed production");

            productionBlocked = blocked;
            productionBlockedReason = blocked ? reason : string.Empty;
        }
    }
}
