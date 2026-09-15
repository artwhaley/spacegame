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
    public class TransportExecutorComponent : MonoBehaviour, ISimulationTickable
    {
        public TransportContract CurrentContract { get; private set; }
        public TransportExecutionState State { get; private set; } = TransportExecutionState.Idle;
        public LocationAnchor CurrentDock { get; private set; }

        private InventoryComponent cargoInventory;
        private PassengerCarrierComponent passengerCarrier;
        private ShipMovementComponent movement;
        private TransportVehicleComponent vehicle;

        private const float QuantityEpsilon = 0.0001f;

        private TransportVehicleComponent Vehicle => vehicle != null
            ? vehicle
            : vehicle = GetComponent<TransportVehicleComponent>();

        public int AboardPassengers => passengerCarrier != null ? passengerCarrier.AboardCount : 0;

        private void Awake()
        {
            cargoInventory = GetComponent<InventoryComponent>();
            passengerCarrier = GetComponent<PassengerCarrierComponent>();
            movement = GetComponent<ShipMovementComponent>();
            vehicle = GetComponent<TransportVehicleComponent>();
            CurrentContract = null;
            State = TransportExecutionState.Idle;
        }

        public void StartContract(TransportContract contract)
        {
            if (contract == null || CurrentContract != null)
                return;

            CurrentContract = contract;
            contract.state = TransportContractState.TravelingToPickup;
            State = TransportExecutionState.TravelingToPickup;
            if (contract.sourceLocation != null)
                SimulationLog.Log($"{GetDisplayName()} traveling to {contract.sourceLocation.displayName}");
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (CurrentContract != null && !CurrentContract.IsActive)
            {
                SimulationLog.Log($"{GetDisplayName()} released contract #{CurrentContract.contractId} (no longer active)");
                CurrentContract = null;
                State = TransportExecutionState.Idle;
            }

            if (CurrentContract == null || Vehicle?.ship == null || !Vehicle.ship.operationalEnabled)
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
            CurrentDock = CurrentContract.sourceLocation;
            State = TransportExecutionState.Loading;
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
                return;

            State = TransportExecutionState.TravelingToDestination;
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
            CurrentDock = CurrentContract.destinationLocation;
            State = TransportExecutionState.Unloading;
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
            CurrentContract = null;
            State = TransportExecutionState.Idle;
            if (ContractManager.Instance != null)
                ContractManager.Instance.Complete(done);
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private string GetDisplayName()
        {
            return Vehicle != null ? Vehicle.DisplayName : name;
        }
    }
}
