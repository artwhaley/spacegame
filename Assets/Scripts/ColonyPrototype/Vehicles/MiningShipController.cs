using UnityEngine;

namespace AsteroidColony
{
    public enum MiningShipState
    {
        Idle,
        TravelingToDeposit,
        Mining,
        ReturningToProcessor,
        Unloading
    }

    /// <summary>
    /// Phase 2's intentionally specialized Ice extraction vehicle. It performs one
    /// direct trip at a time and stores all mined Ice in its local inventory.
    /// </summary>
    public class MiningShipController : MonoBehaviour, ISimulationTickable
    {
        public string displayName = "Mining Ship 1";
        public bool operationalEnabled = true;
        public ColonistAgent assignedPilot;
        public ResourceDefinition collectableResource;
        public ResourceDeposit targetDeposit;
        public LocationAnchor homeUnloadLocation;
        public InventoryComponent destinationInventory;
        public float desiredDestinationIceStock = 24f;
        public float cargoCapacity = 24f;
        public float travelSpeed = 12f;
        public float miningRate = 6f;

        [SerializeField] private MiningShipState state = MiningShipState.Idle;
        [SerializeField] private float missionTargetQuantity;
        [SerializeField] private float missionCollectedQuantity;
        [SerializeField] private float currentCargoQuantity;

        private LocationAnchor shipLocation;
        private InventoryComponent cargoInventory;

        private const float ArrivalEpsilon = 0.05f;
        private const float QuantityEpsilon = 0.0001f;

        public MiningShipState State => state;
        public float CurrentMissionTargetQuantity => missionTargetQuantity;
        public float CurrentCargoQuantity => cargoInventory != null
            ? cargoInventory.GetOnHand(collectableResource)
            : 0f;
        public float CargoCapacity => cargoCapacity;

        private void Awake()
        {
            shipLocation = GetComponent<LocationAnchor>();
            cargoInventory = GetComponent<InventoryComponent>();
            state = MiningShipState.Idle;
            missionTargetQuantity = 0f;
            missionCollectedQuantity = 0f;
            currentCargoQuantity = 0f;
        }

        private void Start()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);

            if (assignedPilot != null && shipLocation != null)
            {
                assignedPilot.MoveToLocation(shipLocation);
                assignedPilot.activity = ColonistActivity.OnDutyCrew;
                SimulationLog.Log($"{assignedPilot.displayName} aboard {displayName}");
            }
        }

        private void OnDisable()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            RefreshObservability();
            // Disabling the ship prevents new missions; an already-running trip
            // finishes coherently and then remains idle until re-enabled.
            switch (state)
            {
                case MiningShipState.Idle:
                    if (operationalEnabled)
                        TryBeginMission();
                    break;

                case MiningShipState.TravelingToDeposit:
                    if (targetDeposit == null || MoveToward(targetDeposit.WorldPosition, deltaGameHours))
                    {
                        state = MiningShipState.Mining;
                        SimulationLog.Log($"{displayName} arrived at {GetDepositName()}");
                        SimulationLog.Log($"{displayName} began mining");
                    }
                    break;

                case MiningShipState.Mining:
                    TickMining(deltaGameHours);
                    break;

                case MiningShipState.ReturningToProcessor:
                    if (homeUnloadLocation == null || MoveToward(homeUnloadLocation.transform.position, deltaGameHours))
                    {
                        state = MiningShipState.Unloading;
                        SimulationLog.Log($"{displayName} returned to {GetHomeName()}");
                    }
                    break;

                case MiningShipState.Unloading:
                    TickUnloading();
                    break;
            }
            RefreshObservability();
        }

        private void TryBeginMission()
        {
            if (assignedPilot == null || targetDeposit == null || destinationInventory == null ||
                targetDeposit.resource != collectableResource ||
                targetDeposit.RemainingQuantity <= QuantityEpsilon || cargoInventory == null)
            {
                return;
            }

            float destinationOnHand = destinationInventory.GetOnHand(collectableResource);
            float iceNeeded = desiredDestinationIceStock - destinationOnHand - CurrentCargoQuantity;
            float freeCargo = Mathf.Max(0f, cargoCapacity - CurrentCargoQuantity);
            float freeDestination = destinationInventory.GetFreeCapacity(collectableResource);
            float missionQuantity = Mathf.Min(
                iceNeeded,
                Mathf.Min(freeCargo, Mathf.Min(freeDestination, targetDeposit.RemainingQuantity)));

            if (missionQuantity <= QuantityEpsilon)
                return;

            missionTargetQuantity = missionQuantity;
            missionCollectedQuantity = 0f;
            state = MiningShipState.TravelingToDeposit;
            SimulationLog.Log($"{displayName} departing for {GetDepositName()}");
        }

        private void TickMining(float deltaGameHours)
        {
            if (targetDeposit == null || cargoInventory == null || destinationInventory == null)
            {
                FinishMiningTrip();
                return;
            }

            float remainingMission = missionTargetQuantity - missionCollectedQuantity;
            float freeCargo = Mathf.Max(0f, cargoCapacity - CurrentCargoQuantity);
            float freeDestination = destinationInventory.GetFreeCapacity(collectableResource);
            float requested = Mathf.Min(
                miningRate * deltaGameHours,
                Mathf.Min(remainingMission, Mathf.Min(freeCargo, freeDestination)));

            if (requested <= QuantityEpsilon)
            {
                FinishMiningTrip();
                return;
            }

            float extracted = targetDeposit.Extract(requested);
            if (extracted > QuantityEpsilon)
            {
                float loaded = cargoInventory.Add(collectableResource, extracted);
                missionCollectedQuantity += loaded;
            }

            if (missionCollectedQuantity + QuantityEpsilon >= missionTargetQuantity ||
                CurrentCargoQuantity + QuantityEpsilon >= cargoCapacity ||
                targetDeposit.RemainingQuantity <= QuantityEpsilon ||
                destinationInventory.GetFreeCapacity(collectableResource) <= QuantityEpsilon)
            {
                FinishMiningTrip();
            }
        }

        private void FinishMiningTrip()
        {
            if (missionCollectedQuantity > QuantityEpsilon)
                SimulationLog.Log($"{displayName} collected {missionCollectedQuantity:0.##} Ice");

            if (CurrentCargoQuantity > QuantityEpsilon)
            {
                state = MiningShipState.ReturningToProcessor;
                SimulationLog.Log($"{displayName} returning to processor");
            }
            else
            {
                state = MiningShipState.Idle;
                missionTargetQuantity = 0f;
                missionCollectedQuantity = 0f;
            }
        }

        private void TickUnloading()
        {
            if (cargoInventory == null || destinationInventory == null)
                return;

            float cargo = CurrentCargoQuantity;
            float freeDestination = destinationInventory.GetFreeCapacity(collectableResource);
            if (cargo <= QuantityEpsilon)
            {
                state = MiningShipState.Idle;
                missionTargetQuantity = 0f;
                missionCollectedQuantity = 0f;
                return;
            }

            float amount = Mathf.Min(cargo, freeDestination);
            if (amount <= QuantityEpsilon)
                return;

            float removed = cargoInventory.Remove(collectableResource, amount);
            float added = destinationInventory.Add(collectableResource, removed);
            if (added < removed - QuantityEpsilon)
                cargoInventory.Add(collectableResource, removed - added);

            if (added > QuantityEpsilon)
                SimulationLog.Log($"{displayName} unloaded {added:0.##} Ice");

            if (CurrentCargoQuantity <= QuantityEpsilon)
            {
                state = MiningShipState.Idle;
                missionTargetQuantity = 0f;
                missionCollectedQuantity = 0f;
            }
        }

        private bool MoveToward(Vector3 target, float deltaGameHours)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, travelSpeed * deltaGameHours);
            return Vector3.Distance(transform.position, target) <= ArrivalEpsilon;
        }

        private string GetDepositName()
        {
            if (targetDeposit == null)
                return "Ice Asteroid 1";
            return string.IsNullOrEmpty(targetDeposit.displayName)
                ? targetDeposit.name
                : targetDeposit.displayName;
        }

        private string GetHomeName()
        {
            return homeUnloadLocation == null ? "Water Processor" : homeUnloadLocation.displayName;
        }

        private void RefreshObservability()
        {
            currentCargoQuantity = cargoInventory != null
                ? cargoInventory.GetOnHand(collectableResource)
                : 0f;
        }
    }
}
