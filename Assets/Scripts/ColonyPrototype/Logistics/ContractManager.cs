using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Owns all transport contracts: generates unique IDs, creates freight and
    /// passenger contracts, exposes open contracts, and marks contracts
    /// assigned/completed/cancelled. Completed contracts stay in the Inspector
    /// list as history.
    /// </summary>
    public class ContractManager : MonoBehaviour
    {
        public static ContractManager Instance { get; private set; }

        [SerializeField] private List<TransportContract> contracts = new List<TransportContract>();
        private int nextContractId = 1;

        public IReadOnlyList<TransportContract> Contracts => contracts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active ContractManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
            ReconcileContractIds();
            ReconcileOrphanedCarriers();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
            ReconcileContractIds();
            ReconcileOrphanedCarriers();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Highest priority first, then earliest creation order.</summary>
        public TransportContract FindBestOpenContract()
        {
            TransportContract best = null;
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract c = contracts[i];
                if (c.state != TransportContractState.Open)
                    continue;
                if (best == null ||
                    c.priority > best.priority ||
                    (c.priority == best.priority && c.contractId < best.contractId))
                {
                    best = c;
                }
            }
            return best;
        }

        public bool HasActiveFreightTo(LocationAnchor destination, ResourceDefinition resource)
        {
            return GetActiveFreightQuantityTo(destination, resource) > 0f;
        }

        /// <summary>
        /// Returns the quantity promised by active freight contracts to a logical
        /// destination. This keeps demand accounting physical without duplicating
        /// stock in a consumer's inventory before the shuttle unloads it.
        /// </summary>
        public float GetActiveFreightQuantityTo(LocationAnchor destination, ResourceDefinition resource)
        {
            float quantity = 0f;
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract c = contracts[i];
                if (c.IsActive &&
                    c.type == TransportContractType.Freight &&
                    c.resource == resource &&
                    c.destinationLocation == destination)
                {
                    quantity += c.RemainingQuantity;
                }
            }
            return quantity;
        }

        /// <summary>
        /// Returns remaining inbound freight associated with one demand snapshot.
        /// Demand-created contracts use this identity so the planner does not
        /// confuse unrelated freight with the demand it is reconciling.
        /// </summary>
        public float GetActiveFreightQuantityForDemand(int demandId)
        {
            if (demandId <= 0)
                return 0f;

            float quantity = 0f;
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract c = contracts[i];
                if (c.IsActive &&
                    c.type == TransportContractType.Freight &&
                    c.demandId == demandId)
                {
                    quantity += c.RemainingQuantity;
                }
            }
            return quantity;
        }

        public bool HasActivePassengerFor(ColonistAgent colonist)
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract c = contracts[i];
                if (!c.IsActive || c.type != TransportContractType.Passenger)
                    continue;
                PrunePassengers(c);
                for (int j = 0; j < c.passengers.Count; j++)
                    if (c.passengers[j] == colonist)
                        return true;
            }
            return false;
        }

        public bool HasActivePassengerForAny(List<ColonistAgent> colonists)
        {
            for (int i = 0; i < colonists.Count; i++)
                if (HasActivePassengerFor(colonists[i]))
                    return true;
            return false;
        }

        /// <summary>
        /// Creates a freight contract and reserves the requested quantity at the
        /// source inventory. If only part is available, the contract covers the
        /// available amount. If zero is available, no contract is created.
        /// </summary>
        public TransportContract CreateFreightContract(
            ResourceDefinition resource, float quantity,
            InventoryComponent sourceInventory, InventoryComponent destinationInventory,
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            int priority = 5)
        {
            return CreateFreightContractInternal(
                0, resource, quantity,
                sourceInventory, destinationInventory,
                sourceLocation, destinationLocation,
                priority, true);
        }

        /// <summary>
        /// Creates a freight contract for a demand after the unified dispatcher has
        /// selected a vehicle. Source reservation happens here, at materialization.
        /// </summary>
        public TransportContract CreateDemandFreightContract(
            int demandId,
            ResourceDefinition resource, float quantity,
            InventoryComponent sourceInventory, InventoryComponent destinationInventory,
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            int priority)
        {
            return CreateFreightContractInternal(
                demandId, resource, quantity,
                sourceInventory, destinationInventory,
                sourceLocation, destinationLocation,
                priority, false);
        }

        private TransportContract CreateFreightContractInternal(
            int demandId,
            ResourceDefinition resource, float quantity,
            InventoryComponent sourceInventory, InventoryComponent destinationInventory,
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            int priority, bool notifyLogistics)
        {
            if (sourceInventory == null || destinationInventory == null ||
                sourceLocation == null || destinationLocation == null)
                return null;

            float available = sourceInventory.GetAvailable(resource);
            float destinationFree = destinationInventory.GetFreeCapacity(resource);
            float toReserve = Mathf.Min(quantity, Mathf.Min(available, destinationFree));
            if (toReserve <= 0f)
                return null;
            if (!sourceInventory.Reserve(resource, toReserve))
                return null;

            TransportContract contract = new TransportContract
            {
                contractId = nextContractId++,
                type = TransportContractType.Freight,
                priority = TransportPriorityRules.Clamp(priority),
                sourceLocation = sourceLocation,
                destinationLocation = destinationLocation,
                state = TransportContractState.Open,
                creationTime = SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f,
                demandId = demandId,
                resource = resource,
                quantity = toReserve,
                sourceInventory = sourceInventory,
                destinationInventory = destinationInventory
            };
            contracts.Add(contract);

            SimulationLog.Log($"Freight contract #{contract.contractId} created: {toReserve:0.###} {resource} at {sourceLocation.displayName} → {destinationLocation.displayName}");
            if (notifyLogistics)
                NotifyLogistics();
            return contract;
        }

        /// <summary>
        /// Creates a passenger contract. All passengers must currently be at the
        /// source location, otherwise no contract is created.
        /// </summary>
        public TransportContract CreatePassengerContract(
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            List<ColonistAgent> passengers, int priority = 10)
        {
            if (passengers == null || passengers.Count == 0 ||
                sourceLocation == null || destinationLocation == null)
            {
                return null;
            }

            for (int i = 0; i < passengers.Count; i++)
            {
                if (passengers[i] == null || passengers[i].currentLocation != sourceLocation)
                    return null;
            }

            TransportContract contract = new TransportContract
            {
                contractId = nextContractId++,
                type = TransportContractType.Passenger,
                priority = TransportPriorityRules.Clamp(priority),
                sourceLocation = sourceLocation,
                destinationLocation = destinationLocation,
                state = TransportContractState.Open,
                creationTime = SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f
            };
            contract.passengers.AddRange(passengers);
            contracts.Add(contract);

            SimulationLog.Log($"Passenger contract #{contract.contractId} created: {sourceLocation.displayName} → {destinationLocation.displayName} ({passengers.Count} passengers)");
            NotifyLogistics();
            return contract;
        }

        /// <summary>
        /// Offers a brand-new contract to logistics immediately, so dispatch does not
        /// wait for the next tick or depend on tickable registration order.
        /// </summary>
        private static void NotifyLogistics()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        public bool Assign(TransportContract contract, TransportVehicleComponent vehicle)
        {
            if (contract == null || vehicle == null ||
                contract.state != TransportContractState.Open ||
                !vehicle.IsAvailable)
                return false;

            if (contract.type == TransportContractType.Freight &&
                contract.quantity > vehicle.GetFreeCargoCapacity(contract.resource) + 0.0001f)
                return false;

            if (contract.type == TransportContractType.Freight && !vehicle.freightEnabled)
                return false;
            if (contract.type == TransportContractType.Passenger && !vehicle.personnelEnabled)
                return false;

            if (contract.type == TransportContractType.Passenger && vehicle.passengerCarrier == null)
                return false;

            if (contract.type == TransportContractType.Passenger)
            {
                string boardingReason;
                if (!vehicle.passengerCarrier.CanBoardPassengers(
                        contract.passengers, contract.sourceLocation, out boardingReason))
                {
                    SimulationLog.Log($"Contract #{contract.contractId} rejected by {vehicle.DisplayName}: {boardingReason}");
                    return false;
                }
            }

            contract.state = TransportContractState.Assigned;
            contract.assignedVehicle = vehicle;
            SimulationLog.Log($"Contract #{contract.contractId} assigned to {vehicle.DisplayName}");
            if (!vehicle.StartContract(contract))
            {
                contract.assignedVehicle = null;
                contract.state = TransportContractState.Open;
                return false;
            }
            return true;
        }

        public void Complete(TransportContract contract)
        {
            if (contract == null)
                return;
            contract.state = TransportContractState.Completed;
            SimulationLog.Log($"Contract #{contract.contractId} completed");
            ReadinessHistory.Record("contract.completed", contract.contractId.ToString());
        }

        public void Cancel(TransportContract contract)
        {
            if (contract == null || contract.state == TransportContractState.Completed ||
                contract.state == TransportContractState.Cancelled)
                return;

            if (contract.type == TransportContractType.Freight &&
                contract.loadedQuantity > contract.deliveredQuantity + 0.0001f)
            {
                SimulationLog.Log($"Contract #{contract.contractId} cannot be cancelled after loading");
                return;
            }

            if (contract.type == TransportContractType.Freight && contract.sourceInventory != null)
            {
                float stillReserved = Mathf.Max(0f, contract.quantity - contract.loadedQuantity);
                contract.sourceInventory.ReleaseReservation(contract.resource, stillReserved);
            }

            contract.assignedVehicle = null;
            contract.state = TransportContractState.Cancelled;
            SimulationLog.Log($"Contract #{contract.contractId} cancelled");
            ReadinessHistory.Record("contract.cancelled", contract.contractId.ToString());
        }

        public void HandleVehicleLoss(TransportContract contract, TransportVehicleComponent vehicle)
        {
            if (contract == null || !contract.IsActive)
                return;

            PassengerCarrierComponent carrier = vehicle != null ? vehicle.passengerCarrier : null;
            LocationAnchor fallback = vehicle != null && vehicle.ship != null
                ? vehicle.ship.CurrentDock : null;
            if (fallback == null)
                fallback = contract.sourceLocation;
            if (carrier != null)
                carrier.RecoverPassengers(fallback);

            contract.assignedVehicle = null;
            if (contract.type == TransportContractType.Freight &&
                contract.loadedQuantity > contract.deliveredQuantity + 0.0001f)
            {
                contract.state = TransportContractState.Cancelled;
                SimulationLog.Log($"Contract #{contract.contractId} cancelled after vehicle loss with loaded freight");
            }
            else if (contract.type == TransportContractType.Passenger && fallback == contract.sourceLocation)
            {
                contract.state = TransportContractState.Open;
                SimulationLog.Log($"Contract #{contract.contractId} reopened after vehicle loss");
            }
            else
            {
                Cancel(contract);
            }
            ReadinessHistory.Record("vehicle.loss", vehicle != null ? vehicle.DisplayName : "<vehicle>",
                contract.contractId.ToString());
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private void ReconcileContractIds()
        {
            for (int i = 0; i < contracts.Count; i++)
                if (contracts[i] != null)
                    nextContractId = Mathf.Max(nextContractId, contracts[i].contractId + 1);
        }

        private void ReconcileOrphanedCarriers()
        {
            PassengerCarrierComponent[] carriers = FindObjectsByType<PassengerCarrierComponent>(FindObjectsInactive.Include);
            for (int i = 0; i < carriers.Length; i++)
            {
                PassengerCarrierComponent carrier = carriers[i];
                if (carrier == null)
                    continue;
                carrier.PruneDestroyedPassengers();
                bool hasObligation = false;
                IReadOnlyList<ColonistAgent> passengers = carrier.CurrentPassengers;
                for (int p = 0; p < passengers.Count && !hasObligation; p++)
                    hasObligation = HasActivePassengerFor(passengers[p]);
                if (!hasObligation && passengers.Count > 0)
                    carrier.RecoverPassengers(carrier.CarrierLocation);
            }
        }

        private static void PrunePassengers(TransportContract contract)
        {
            if (contract.passengers == null)
                contract.passengers = new List<ColonistAgent>();
            for (int i = contract.passengers.Count - 1; i >= 0; i--)
                if (contract.passengers[i] == null)
                    contract.passengers.RemoveAt(i);
        }
    }
}
