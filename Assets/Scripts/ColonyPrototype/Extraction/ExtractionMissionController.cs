using UnityEngine;

namespace AsteroidColony
{
    public enum ExtractionMissionState
    {
        Idle,
        TravelingToDeposit,
        Extracting,
        Returning,
        Unloading
    }

    /// <summary>Orchestrates one extraction trip without ordinary logistics contracts.</summary>
    public class ExtractionMissionController : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public ShipComponent ship;
        public ShipMovementComponent movement;
        public ResourceCollectorComponent collector;
        public ResourceDeposit targetDeposit;
        public LocationAnchor unloadLocation;
        public InventoryComponent unloadInventory;
        public ResourceStockPolicyComponent destinationStockPolicy;
        public float fallbackTargetStock = 24f;

        [SerializeField] private ExtractionMissionState state = ExtractionMissionState.Idle;
        [SerializeField] private float missionTargetQuantity;
        [SerializeField] private float missionCollectedQuantity;
        [SerializeField] private string lastDiagnostic;

        private const float QuantityEpsilon = 0.0001f;

        public ExtractionMissionState State => state;
        public int SimulationTickPriority => 400;
        public float CurrentMissionTargetQuantity => missionTargetQuantity;
        public float MissionCollectedQuantity => missionCollectedQuantity;
        public string LastDiagnostic => lastDiagnostic;
        public float CurrentCargoQuantity => collector != null && collector.destinationCargo != null &&
            collector.collectableResource != null
            ? collector.destinationCargo.GetOnHand(collector.collectableResource)
            : 0f;

        private void Awake()
        {
            if (ship == null)
                ship = GetComponent<ShipComponent>();
            if (movement == null)
                movement = GetComponent<ShipMovementComponent>();
            if (collector == null)
                collector = GetComponent<ResourceCollectorComponent>();
            if (unloadInventory == null && unloadLocation != null)
                unloadInventory = unloadLocation.GetComponent<InventoryComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (state != ExtractionMissionState.Idle)
                SetDiagnostic("extraction operation disabled; mission paused");
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (!isActiveAndEnabled || (ship != null && (!ship.isActiveAndEnabled || !ship.operationalEnabled ||
                (ship.crewStaffing != null && !ship.crewStaffing.isActiveAndEnabled))))
                return;
            switch (state)
            {
                case ExtractionMissionState.Idle:
                    if (ship != null && ship.operationalEnabled)
                        TryBeginMission();
                    break;

                case ExtractionMissionState.TravelingToDeposit:
                    if (targetDeposit == null)
                    {
                        SetDiagnostic("missing extraction deposit");
                        break;
                    }
                    if (movement == null || !movement.isActiveAndEnabled)
                    {
                        SetDiagnostic("missing or disabled ship movement component");
                        break;
                    }
                    if (movement.MoveToward(targetDeposit.WorldPosition, deltaGameHours))
                    {
                        ship.BeginDocking(null);
                        // A deposit has no logical dock identity. The extraction
                        // movement lease still gates the hard arrival transition;
                        // keep the ship undocked while the collector works.
                        state = ExtractionMissionState.Extracting;
                        SimulationLog.Log($"{GetDisplayName()} arrived at {GetDepositName()}");
                        SimulationLog.Log($"{GetDisplayName()} began extraction");
                    }
                    break;

                case ExtractionMissionState.Extracting:
                    TickExtraction(deltaGameHours);
                    break;

                case ExtractionMissionState.Returning:
                    if (unloadLocation == null)
                    {
                        SetDiagnostic("missing extraction unload location");
                        break;
                    }
                    if (movement == null || !movement.isActiveAndEnabled)
                    {
                        SetDiagnostic("missing or disabled ship movement component");
                        break;
                    }
                    if (movement.MoveToward(unloadLocation, deltaGameHours))
                    {
                        if (ship == null || !ship.TryCompleteArrival(ShipMovementOwner.Extraction, unloadLocation))
                        {
                            SetDiagnostic("extraction arrival was not owned by this mission");
                            break;
                        }
                        state = ExtractionMissionState.Unloading;
                        SimulationLog.Log($"{GetDisplayName()} returned to {GetUnloadName()}");
                    }
                    break;

                case ExtractionMissionState.Unloading:
                    TickUnloading();
                    break;
            }
        }

        private void TryBeginMission()
        {
            if (ship == null || !ship.IsOperationallyCrewed || !ship.HasSafeDock || ship.IsTraveling ||
                movement == null || !movement.isActiveAndEnabled ||
                ship.ReleaseRequested || collector == null ||
                collector.collectableResource == null || targetDeposit == null ||
                targetDeposit.resource != collector.collectableResource ||
                !targetDeposit.extractionEnabled || targetDeposit.RemainingQuantity <= QuantityEpsilon ||
                collector.destinationCargo == null || unloadInventory == null)
                return;

            float desiredStock = GetDestinationTargetStock();
            float destinationOnHand = unloadInventory.GetOnHand(collector.collectableResource);
            float cargoOnHand = CurrentCargoQuantity;
            float needed = desiredStock - destinationOnHand - cargoOnHand;
            float freeCargo = collector.destinationCargo.GetFreeCapacity(collector.collectableResource);
            float freeDestination = unloadInventory.GetFreeCapacity(collector.collectableResource);
            float missionQuantity = Mathf.Min(needed,
                Mathf.Min(freeCargo, Mathf.Min(freeDestination, targetDeposit.RemainingQuantity)));

            if (missionQuantity <= QuantityEpsilon)
                return;

            missionTargetQuantity = missionQuantity;
            missionCollectedQuantity = 0f;
            if (!ship.TryClaimMovement(ShipMovementOwner.Extraction))
                return;
            state = ExtractionMissionState.TravelingToDeposit;
            ship.BeginUndocking();
            ship.MarkDeparted();
            SimulationLog.Log($"{GetDisplayName()} departing for {GetDepositName()}");
        }

        private void TickExtraction(float deltaGameHours)
        {
            if (collector == null || targetDeposit == null || unloadInventory == null)
            {
                FinishExtractionTrip();
                return;
            }

            float remainingMission = missionTargetQuantity - missionCollectedQuantity;
            float freeCargo = collector.destinationCargo.GetFreeCapacity(collector.collectableResource);
            float freeDestination = unloadInventory.GetFreeCapacity(collector.collectableResource);
            if (remainingMission <= QuantityEpsilon || freeCargo <= QuantityEpsilon ||
                freeDestination <= QuantityEpsilon || targetDeposit.RemainingQuantity <= QuantityEpsilon)
            {
                FinishExtractionTrip();
                return;
            }

            float collected = collector.Collect(targetDeposit, deltaGameHours);
            missionCollectedQuantity += collected;
            if (missionCollectedQuantity + QuantityEpsilon >= missionTargetQuantity ||
                collector.destinationCargo.GetFreeCapacity(collector.collectableResource) <= QuantityEpsilon ||
                targetDeposit.RemainingQuantity <= QuantityEpsilon ||
                unloadInventory.GetFreeCapacity(collector.collectableResource) <= QuantityEpsilon)
                FinishExtractionTrip();
        }

        private void FinishExtractionTrip()
        {
            if (missionCollectedQuantity > QuantityEpsilon)
                SimulationLog.Log($"{GetDisplayName()} collected {missionCollectedQuantity:0.##} {collector.collectableResource.displayName}");

            if (CurrentCargoQuantity > QuantityEpsilon)
            {
                state = ExtractionMissionState.Returning;
                SimulationLog.Log($"{GetDisplayName()} returning to {GetUnloadName()}");
            }
            else
                ResetMission();
        }

        private void TickUnloading()
        {
            if (collector == null || collector.destinationCargo == null || unloadInventory == null)
                return;

            float cargo = CurrentCargoQuantity;
            if (cargo <= QuantityEpsilon)
            {
                ResetMission();
                return;
            }

            float amount = Mathf.Min(cargo, unloadInventory.GetFreeCapacity(collector.collectableResource));
            if (amount <= QuantityEpsilon)
                return;

            float removed = collector.destinationCargo.Remove(collector.collectableResource, amount);
            float added = unloadInventory.Add(collector.collectableResource, removed);
            if (added < removed - QuantityEpsilon)
                collector.destinationCargo.Add(collector.collectableResource, removed - added);
            if (added > QuantityEpsilon)
                SimulationLog.Log($"{GetDisplayName()} unloaded {added:0.##} {collector.collectableResource.displayName}");

            if (CurrentCargoQuantity <= QuantityEpsilon)
                ResetMission();
        }

        private void ResetMission()
        {
            state = ExtractionMissionState.Idle;
            missionTargetQuantity = 0f;
            missionCollectedQuantity = 0f;
            if (ship != null)
                ship.ReleaseMovement(ShipMovementOwner.Extraction);
            lastDiagnostic = string.Empty;
        }

        public bool CancelMission()
        {
            if (state == ExtractionMissionState.Idle)
                return false;
            ResetMission();
            ReadinessHistory.Record("extraction.cancelled", GetDisplayName());
            return true;
        }

        private void SetDiagnostic(string message)
        {
            message = message ?? string.Empty;
            if (lastDiagnostic == message)
                return;
            lastDiagnostic = message;
            if (!string.IsNullOrEmpty(message))
                ReadinessHistory.Record("extraction.blocked", GetDisplayName(), message);
        }

        private float GetDestinationTargetStock()
        {
            if (destinationStockPolicy != null && collector != null)
            {
                for (int i = 0; i < destinationStockPolicy.entries.Count; i++)
                {
                    ResourceStockPolicyEntry entry = destinationStockPolicy.entries[i];
                    if (entry != null && entry.resource == collector.collectableResource)
                        return Mathf.Max(0f, entry.targetStock);
                }
            }
            return Mathf.Max(0f, fallbackTargetStock);
        }

        private string GetDisplayName()
        {
            return ship != null ? ship.displayName : name;
        }

        private string GetDepositName()
        {
            if (targetDeposit == null)
                return "deposit";
            return string.IsNullOrEmpty(targetDeposit.displayName) ? targetDeposit.name : targetDeposit.displayName;
        }

        private string GetUnloadName()
        {
            return unloadLocation == null ? "destination" : unloadLocation.displayName;
        }
    }
}
