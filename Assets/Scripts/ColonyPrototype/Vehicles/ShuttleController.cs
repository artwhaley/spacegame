using UnityEngine;

namespace AsteroidColony
{
    public enum ShuttleState
    {
        Idle,
        TravelingToPickup,
        Loading,
        TravelingToDestination,
        Unloading
    }

    /// <summary>
    /// Executes one transport contract at a time using direct transform movement
    /// that responds to simulation time. No physics, no pathfinding, no docking.
    /// The shuttle remains wherever its previous contract ended.
    /// </summary>
    public class ShuttleController : MonoBehaviour, ISimulationTickable
    {
        public string displayName;
        public bool operationalEnabled = true;
        public ColonistAgent assignedPilot;
        public LocationAnchor currentDock;
        public int passengerCapacity = 4;
        public float cargoCapacity = 12f;
        public float movementSpeed = 12f;

        /// <summary>
        /// The contract this shuttle is currently executing. Runtime-only: a job is
        /// never scene data. If this were serialized, any stale value saved in the
        /// scene would be read back as a real contract and the shuttle would report
        /// itself busy forever, silently blocking all dispatch.
        /// </summary>
        [System.NonSerialized] public TransportContract currentContract;

        [SerializeField] private ShuttleState state = ShuttleState.Idle;
        [SerializeField] private int aboardPassengers;
        [SerializeField] private int currentContractId;
        [SerializeField] private TransportContractType currentContractType;
        [SerializeField] private ResourceType currentContractResource;
        [SerializeField] private float currentContractQuantity;
        [SerializeField] private LocationAnchor currentContractSource;
        [SerializeField] private LocationAnchor currentContractDestination;
        [SerializeField] private TransportContractState currentContractState;
        [SerializeField] private float foodCargo;
        [SerializeField] private float iceCargo;
        [SerializeField] private float waterCargo;

        public ShuttleState State => state;
        public int AboardPassengers => aboardPassengers;

        private LocationAnchor shuttleAnchor;
        private InventoryComponent cargoInventory;

        private const float ArrivalEpsilon = 0.05f;

        private void Awake()
        {
            shuttleAnchor = GetComponent<LocationAnchor>();
            cargoInventory = GetComponent<InventoryComponent>();

            // Runtime state starts clean every run; never inherit it from the scene.
            currentContract = null;
            state = ShuttleState.Idle;
            aboardPassengers = 0;
            RefreshObservability();
        }

        private bool started;

        private void Start()
        {
            started = true;
            RegisterWithManagers();

            if (LogisticsManager.Instance != null)
                SimulationLog.Log($"{displayName} registered with LogisticsManager");
            else
                SimulationLog.Log($"{displayName}: LogisticsManager not found at startup");

            // Pilot 1 is transferred aboard the shuttle during initialization.
            if (assignedPilot != null && shuttleAnchor != null)
            {
                assignedPilot.MoveToLocation(shuttleAnchor);
                assignedPilot.activity = ColonistActivity.OnDutyCrew;
                SimulationLog.Log($"{assignedPilot.displayName} aboard {displayName}");
            }
        }

        private void OnEnable()
        {
            // Symmetric with OnDisable: a shuttle switched off and back on must rejoin the
            // fleet, or it is gone for the rest of the run. Registration is idempotent, so
            // the initial enable (which runs before Start, and possibly before the managers'
            // Awake) is harmless and Start covers it if the managers were not ready yet.
            RegisterWithManagers();
            if (!started)
                return;

            SimulationLog.Log($"{displayName} back online");
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private void OnDisable()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.UnregisterShuttle(this);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        private void RegisterWithManagers()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.RegisterShuttle(this);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        /// <summary>True while the shuttle is executing a live contract.</summary>
        public bool HasActiveWork => currentContract != null && currentContract.IsActive;

        /// <summary>Available for new work only when operational, piloted, and idle.</summary>
        public bool IsAvailable =>
            operationalEnabled && assignedPilot != null && !HasActiveWork;

        /// <summary>
        /// Returns the cargo space available for one resource. The per-resource
        /// InventoryComponent capacity is the authoritative limit; cargoCapacity
        /// remains the vehicle-level cap used by the prototype.
        /// </summary>
        public float GetFreeCargoCapacity(ResourceType resource)
        {
            float vehicleFree = Mathf.Max(0f, cargoCapacity);
            if (cargoInventory == null)
                return vehicleFree;
            return Mathf.Min(vehicleFree, cargoInventory.GetFreeCapacity(resource));
        }

        /// <summary>Short human-readable reason this shuttle cannot take new work.</summary>
        public string AvailabilityBlocker()
        {
            if (!operationalEnabled)
                return "operational disabled";
            if (assignedPilot == null)
                return "no pilot assigned";
            if (HasActiveWork)
                return $"busy with contract #{currentContract.contractId}";
            return "available";
        }

        public void StartContract(TransportContract contract)
        {
            currentContract = contract;
            contract.state = TransportContractState.TravelingToPickup;
            state = ShuttleState.TravelingToPickup;
            if (contract.sourceLocation != null)
                SimulationLog.Log($"{displayName} traveling to {contract.sourceLocation.displayName}");
            RefreshObservability();
        }

        public void SimulationTick(float deltaGameHours)
        {
            // A contract completed or cancelled by someone else must never leave the
            // shuttle locked out of new work.
            if (currentContract != null && !currentContract.IsActive)
            {
                SimulationLog.Log($"{displayName} released contract #{currentContract.contractId} (no longer active)");
                currentContract = null;
                state = ShuttleState.Idle;
                aboardPassengers = 0;
            }

            if (currentContract == null || !operationalEnabled)
            {
                RefreshObservability();
                return;
            }

            switch (state)
            {
                case ShuttleState.TravelingToPickup:
                    if (MoveToward(currentContract.sourceLocation, deltaGameHours))
                        BeginLoading();
                    break;

                case ShuttleState.TravelingToDestination:
                    if (MoveToward(currentContract.destinationLocation, deltaGameHours))
                        BeginUnloading();
                    break;

                case ShuttleState.Unloading:
                    if (currentContract.type != TransportContractType.Freight || TryUnloadFreight())
                        CompleteCurrentContract();
                    break;
            }
            RefreshObservability();
        }

        private bool MoveToward(LocationAnchor target, float deltaGameHours)
        {
            if (target == null)
                return true;
            Vector3 destination = target.transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position, destination, movementSpeed * deltaGameHours);
            return Vector3.Distance(transform.position, destination) <= ArrivalEpsilon;
        }

        private void BeginLoading()
        {
            currentDock = currentContract.sourceLocation;
            state = ShuttleState.Loading;
            currentContract.state = TransportContractState.Loading;

            if (currentContract.type == TransportContractType.Freight)
                LoadFreight();
            else
                LoadPassengers();

            state = ShuttleState.TravelingToDestination;
            currentContract.state = TransportContractState.TravelingToDestination;
            SimulationLog.Log($"{displayName} departed {currentContract.sourceLocation.displayName}");
        }

        private void LoadFreight()
        {
            float requested = currentContract.quantity;
            float withdrawn = currentContract.sourceInventory != null
                ? currentContract.sourceInventory.WithdrawReserved(currentContract.resourceType, requested)
                : 0f;
            float loaded = cargoInventory != null
                ? cargoInventory.Add(currentContract.resourceType, withdrawn)
                : 0f;
            if (loaded < withdrawn - 0.0001f && currentContract.sourceInventory != null)
                currentContract.sourceInventory.Add(currentContract.resourceType, withdrawn - loaded);

            currentContract.loadedQuantity = loaded;
            if (loaded < requested - 0.0001f)
                currentContract.quantity = loaded;
            SimulationLog.Log($"{displayName} loaded {loaded} {currentContract.resourceType} at {currentContract.sourceLocation.displayName}");
        }

        private void LoadPassengers()
        {
            aboardPassengers = 0;
            for (int i = 0; i < currentContract.passengers.Count; i++)
            {
                ColonistAgent passenger = currentContract.passengers[i];
                if (passenger == null)
                    continue;
                passenger.MoveToLocation(shuttleAnchor);
                passenger.activity = ColonistActivity.Passenger;
                aboardPassengers++;
                SimulationLog.Log($"{passenger.displayName} boarded {displayName}");
            }
        }

        private void BeginUnloading()
        {
            currentDock = currentContract.destinationLocation;
            state = ShuttleState.Unloading;
            currentContract.state = TransportContractState.Unloading;

            bool complete = true;
            if (currentContract.type == TransportContractType.Freight)
                complete = TryUnloadFreight();
            else
                UnloadPassengers();

            SimulationLog.Log($"{displayName} arrived {currentContract.destinationLocation.displayName}");

            if (!complete)
                return;

            CompleteCurrentContract();
        }

        private void CompleteCurrentContract()
        {
            if (currentContract == null)
                return;

            TransportContract done = currentContract;
            currentContract = null;
            state = ShuttleState.Idle;
            aboardPassengers = 0;
            if (ContractManager.Instance != null)
                ContractManager.Instance.Complete(done);

            // Capacity just freed up: let logistics dispatch the next waiting job now
            // rather than waiting for the next tick.
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();

            RefreshObservability();
        }

        private bool TryUnloadFreight()
        {
            if (cargoInventory == null || currentContract.destinationInventory == null)
                return false;

            float cargo = cargoInventory.GetOnHand(currentContract.resourceType);
            if (cargo <= 0.0001f)
                return true;

            float amount = Mathf.Min(cargo,
                currentContract.destinationInventory.GetFreeCapacity(currentContract.resourceType));
            if (amount <= 0.0001f)
                return false;

            float removed = cargoInventory.Remove(currentContract.resourceType, amount);
            float added = currentContract.destinationInventory.Add(currentContract.resourceType, removed);
            if (added < removed - 0.0001f)
                cargoInventory.Add(currentContract.resourceType, removed - added);
            if (added > 0f)
            {
                currentContract.deliveredQuantity += added;
                SimulationLog.Log($"{displayName} delivered {added} {currentContract.resourceType} to {currentContract.destinationLocation.displayName}");
            }

            return cargoInventory.GetOnHand(currentContract.resourceType) <= 0.0001f;
        }

        private void UnloadPassengers()
        {
            aboardPassengers = 0;
            for (int i = 0; i < currentContract.passengers.Count; i++)
            {
                ColonistAgent passenger = currentContract.passengers[i];
                if (passenger == null)
                    continue;
                passenger.MoveToLocation(currentContract.destinationLocation);
                passenger.activity = ColonistActivity.Idle;
                SimulationLog.Log($"{passenger.displayName} unloaded at {currentContract.destinationLocation.displayName}");
            }
        }

        private void RefreshObservability()
        {
            currentContractId = currentContract != null ? currentContract.contractId : 0;
            currentContractType = currentContract != null
                ? currentContract.type
                : TransportContractType.Freight;
            currentContractResource = currentContract != null
                ? currentContract.resourceType
                : ResourceType.Food;
            currentContractQuantity = currentContract != null ? currentContract.RemainingQuantity : 0f;
            currentContractSource = currentContract != null ? currentContract.sourceLocation : null;
            currentContractDestination = currentContract != null ? currentContract.destinationLocation : null;
            currentContractState = currentContract != null
                ? currentContract.state
                : TransportContractState.Completed;
            foodCargo = cargoInventory != null ? cargoInventory.GetOnHand(ResourceType.Food) : 0f;
            iceCargo = cargoInventory != null ? cargoInventory.GetOnHand(ResourceType.Ice) : 0f;
            waterCargo = cargoInventory != null ? cargoInventory.GetOnHand(ResourceType.Water) : 0f;
        }
    }
}
