using UnityEngine;

namespace AsteroidColony
{
    public enum TransportExecutionState
    {
        Idle,
        TravelingToPickup,
        Loading,
        TravelingToDestination,
        Unloading
    }

    /// <summary>Executes the physical steps of one transport contract.</summary>
    public class TransportExecutorComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        [SerializeField] private TransportContract currentContract;
        [SerializeField] private TransportExecutionState state = TransportExecutionState.Idle;

        public TransportContract CurrentContract => currentContract;
        public TransportExecutionState State => state;
        public LocationAnchor CurrentDock => Vehicle != null && Vehicle.ship != null
            ? Vehicle.ship.CurrentDock
            : null;

        private InventoryComponent cargoInventory;
        private PassengerCarrierComponent passengerCarrier;
        private ShipMovementComponent movement;
        private TransportVehicleComponent vehicle;

        private const float QuantityEpsilon = 0.0001f;

        private TransportVehicleComponent Vehicle => vehicle != null
            ? vehicle
            : vehicle = GetComponent<TransportVehicleComponent>();

        public int AboardPassengers => passengerCarrier != null ? passengerCarrier.AboardCount : 0;
        public int SimulationTickPriority => 400;

        private void Awake()
        {
            cargoInventory = GetComponent<InventoryComponent>();
            passengerCarrier = GetComponent<PassengerCarrierComponent>();
            movement = GetComponent<ShipMovementComponent>();
            vehicle = GetComponent<TransportVehicleComponent>();
            // Do not clear an active contract here. Components can be disabled and
            // re-enabled while the scene is being inspected; serialized state is
            // authoritative and the next simulation tick resumes it.
            if (currentContract == null || !currentContract.IsAssignedOrInFlight)
            {
                currentContract = null;
                state = TransportExecutionState.Idle;
            }
            else
            {
                state = StateForContract(currentContract.state);
            }
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void StartContract(TransportContract contract)
        {
            if (contract == null || contract.contractId <= 0 || CurrentContract != null)
                return;

            currentContract = contract;
            contract.state = TransportContractState.TravelingToPickup;
            state = TransportExecutionState.TravelingToPickup;
            if (Vehicle != null && Vehicle.ship != null && !Vehicle.ship.TryClaimMovement(ShipMovementOwner.Transport))
            {
                currentContract = null;
                state = TransportExecutionState.Idle;
                contract.state = TransportContractState.Open;
                return;
            }
            if (Vehicle != null && Vehicle.ship != null)
                Vehicle.ship.MarkDeparted();
            if (contract.sourceLocation != null)
                SimulationLog.Log($"{GetDisplayName()} traveling to {contract.sourceLocation.displayName}");
        }

        public void SimulationTick(float deltaGameHours)
        {
            // A serialized/default TransportContract that was never registered
            // by ContractManager cannot make progress and would strand the
            // vehicle forever (the default contractId is zero). Treat it as
            // stale runtime state and release the transport movement lease.
            if (CurrentContract != null && CurrentContract.contractId <= 0)
            {
                SimulationLog.Log($"{GetDisplayName()} discarded stale transport contract #{CurrentContract.contractId}");
                currentContract = null;
                state = TransportExecutionState.Idle;
                if (Vehicle != null && Vehicle.ship != null)
                    Vehicle.ship.ReleaseMovement(ShipMovementOwner.Transport);
            }
            if (CurrentContract != null && !CurrentContract.IsAssignedOrInFlight)
            {
                SimulationLog.Log($"{GetDisplayName()} released contract #{CurrentContract.contractId} (no longer active)");
                currentContract = null;
                state = TransportExecutionState.Idle;
                if (Vehicle != null && Vehicle.ship != null)
                    Vehicle.ship.ReleaseMovement(ShipMovementOwner.Transport);
            }

            if (CurrentContract == null || Vehicle == null || !Vehicle.isActiveAndEnabled || Vehicle.ship == null ||
                !Vehicle.ship.isActiveAndEnabled || !Vehicle.ship.operationalEnabled ||
                (Vehicle.ship.crewStaffing != null && !Vehicle.ship.crewStaffing.isActiveAndEnabled))
                return;

            switch (State)
            {
                case TransportExecutionState.TravelingToPickup:
                    if (movement == null || movement.MoveToward(CurrentContract.sourceLocation, deltaGameHours))
                        BeginLoading();
                    break;

                case TransportExecutionState.Loading:
                    CompleteLoading();
                    break;

                case TransportExecutionState.TravelingToDestination:
                    if (movement == null || movement.MoveToward(CurrentContract.destinationLocation, deltaGameHours))
                        BeginUnloading();
                    break;

                case TransportExecutionState.Unloading:
                    bool unloaded = CurrentContract.type == TransportContractType.Freight
                        ? TryUnloadFreight()
                        : passengerCarrier != null && passengerCarrier.TryUnboardPassengers(CurrentContract.destinationLocation);
                    if (unloaded)
                        CompleteCurrentContract();
                    break;
            }
        }

        private void BeginLoading()
        {
            if (Vehicle != null && Vehicle.ship != null)
                Vehicle.ship.SetDock(CurrentContract.sourceLocation);
            state = TransportExecutionState.Loading;
            CurrentContract.state = TransportContractState.Loading;
            CompleteLoading();
        }

        private void CompleteLoading()
        {
            if (CurrentContract == null)
                return;

            bool loaded = CurrentContract.type == TransportContractType.Freight
                ? LoadFreight()
                : LoadPassengers();
            if (!loaded)
            {
                if (CurrentContract.type == TransportContractType.Passenger && passengerCarrier != null)
                {
                    string boardingReason;
                    if (!passengerCarrier.CanBoardPassengers(
                            CurrentContract.passengers, CurrentContract.sourceLocation, out boardingReason))
                    {
                        SimulationLog.Log($"{GetDisplayName()} cancelled contract #{CurrentContract.contractId} before boarding: {boardingReason}");
                        if (ContractManager.Instance != null)
                            ContractManager.Instance.Cancel(CurrentContract);
                        currentContract = null;
                        state = TransportExecutionState.Idle;
                        if (Vehicle != null && Vehicle.ship != null)
                            Vehicle.ship.ReleaseMovement(ShipMovementOwner.Transport);
                        if (LogisticsManager.Instance != null)
                            LogisticsManager.Instance.TryAssignNext();
                    }
                }
                return;
            }

            if (Vehicle != null && Vehicle.ship != null)
                Vehicle.ship.MarkDeparted();

            state = TransportExecutionState.TravelingToDestination;
            CurrentContract.state = TransportContractState.TravelingToDestination;
            SimulationLog.Log($"{GetDisplayName()} departed {CurrentContract.sourceLocation.displayName}");
        }

        private bool LoadFreight()
        {
            float requested = CurrentContract.quantity;
            float withdrawn = CurrentContract.sourceInventory != null
                ? CurrentContract.sourceInventory.WithdrawReserved(CurrentContract.resource, requested)
                : 0f;
            float loaded = cargoInventory != null
                ? cargoInventory.Add(CurrentContract.resource, withdrawn)
                : 0f;
            if (loaded < withdrawn - QuantityEpsilon && CurrentContract.sourceInventory != null)
                CurrentContract.sourceInventory.Add(CurrentContract.resource, withdrawn - loaded);

            CurrentContract.loadedQuantity = loaded;
            if (loaded < requested - QuantityEpsilon)
                CurrentContract.quantity = loaded;
            SimulationLog.Log($"{GetDisplayName()} loaded {loaded} {CurrentContract.resource} at {CurrentContract.sourceLocation.displayName}");
            return true;
        }

        private bool LoadPassengers()
        {
            if (passengerCarrier == null ||
                !passengerCarrier.TryBoardPassengers(CurrentContract.passengers, CurrentContract.sourceLocation))
                return false;

            for (int i = 0; i < CurrentContract.passengers.Count; i++)
            {
                ColonistAgent passenger = CurrentContract.passengers[i];
                if (passenger != null)
                    SimulationLog.Log($"{passenger.displayName} boarded {GetDisplayName()}");
            }
            return true;
        }

        private void BeginUnloading()
        {
            if (Vehicle != null && Vehicle.ship != null)
                Vehicle.ship.SetDock(CurrentContract.destinationLocation);
            state = TransportExecutionState.Unloading;
            CurrentContract.state = TransportContractState.Unloading;

            bool complete = CurrentContract.type == TransportContractType.Freight
                ? TryUnloadFreight()
                : passengerCarrier != null && passengerCarrier.TryUnboardPassengers(CurrentContract.destinationLocation);
            SimulationLog.Log($"{GetDisplayName()} arrived {CurrentContract.destinationLocation.displayName}");
            if (complete)
                CompleteCurrentContract();
        }

        private bool TryUnloadFreight()
        {
            if (cargoInventory == null || CurrentContract.destinationInventory == null)
                return false;

            float cargo = cargoInventory.GetOnHand(CurrentContract.resource);
            if (cargo <= QuantityEpsilon)
                return true;

            float amount = Mathf.Min(cargo,
                CurrentContract.destinationInventory.GetFreeCapacity(CurrentContract.resource));
            if (amount <= QuantityEpsilon)
                return false;

            float removed = cargoInventory.Remove(CurrentContract.resource, amount);
            float added = CurrentContract.destinationInventory.Add(CurrentContract.resource, removed);
            if (added < removed - QuantityEpsilon)
                cargoInventory.Add(CurrentContract.resource, removed - added);
            if (added > 0f)
            {
                CurrentContract.deliveredQuantity += added;
                SimulationLog.Log($"{GetDisplayName()} delivered {added} {CurrentContract.resource} to {CurrentContract.destinationLocation.displayName}");
            }
            return cargoInventory.GetOnHand(CurrentContract.resource) <= QuantityEpsilon;
        }

        private void CompleteCurrentContract()
        {
            if (CurrentContract == null)
                return;

            TransportContract done = CurrentContract;
            currentContract = null;
            state = TransportExecutionState.Idle;
            if (Vehicle != null && Vehicle.ship != null)
                Vehicle.ship.ReleaseMovement(ShipMovementOwner.Transport);
            if (ContractManager.Instance != null)
                ContractManager.Instance.Complete(done);
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private static TransportExecutionState StateForContract(TransportContractState contractState)
        {
            switch (contractState)
            {
                case TransportContractState.Assigned:
                case TransportContractState.TravelingToPickup:
                    return TransportExecutionState.TravelingToPickup;
                case TransportContractState.Loading:
                    return TransportExecutionState.Loading;
                case TransportContractState.TravelingToDestination:
                    return TransportExecutionState.TravelingToDestination;
                case TransportContractState.Unloading:
                    return TransportExecutionState.Unloading;
                default:
                    return TransportExecutionState.Idle;
            }
        }

        private string GetDisplayName()
        {
            return Vehicle != null ? Vehicle.DisplayName : name;
        }
    }
}
