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
            Instance = this;
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

        public bool HasActiveFreightTo(LocationAnchor destination, ResourceType resource)
        {
            return GetActiveFreightQuantityTo(destination, resource) > 0f;
        }

        /// <summary>
        /// Returns the quantity promised by active freight contracts to a logical
        /// destination. This keeps demand accounting physical without duplicating
        /// stock in a consumer's inventory before the shuttle unloads it.
        /// </summary>
        public float GetActiveFreightQuantityTo(LocationAnchor destination, ResourceType resource)
        {
            float quantity = 0f;
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract c = contracts[i];
                if (c.IsActive &&
                    c.type == TransportContractType.Freight &&
                    c.resourceType == resource &&
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
            ResourceType resource, float quantity,
            InventoryComponent sourceInventory, InventoryComponent destinationInventory,
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            int priority = 50)
        {
            return CreateFreightContractInternal(
                0, resource, quantity,
                sourceInventory, destinationInventory,
                sourceLocation, destinationLocation,
                priority, true);
        }

        /// <summary>
        /// Creates and assigns one planner-approved freight contract. The planner
        /// selects the shuttle first so contract quantity can respect its remaining
        /// cargo capacity and no waiting micro-contract is created.
        /// </summary>
        public TransportContract CreateAssignedFreightContract(
            int demandId,
            ResourceType resource, float quantity,
            InventoryComponent sourceInventory, InventoryComponent destinationInventory,
            LocationAnchor sourceLocation, LocationAnchor destinationLocation,
            ShuttleController shuttle, int priority, float minimumShipment)
        {
            if (shuttle == null || !shuttle.IsAvailable)
                return null;

            float shuttleCapacity = shuttle.GetFreeCargoCapacity(resource);
            float cappedQuantity = Mathf.Min(quantity, shuttleCapacity);
            if (cappedQuantity < Mathf.Max(0f, minimumShipment) - 0.0001f)
                return null;

            TransportContract contract = CreateFreightContractInternal(
                demandId, resource, cappedQuantity,
                sourceInventory, destinationInventory,
                sourceLocation, destinationLocation,
                priority, false);
            if (contract == null)
                return null;

            if (contract.quantity < Mathf.Max(0f, minimumShipment) - 0.0001f)
            {
                Cancel(contract);
                return null;
            }

            Assign(contract, shuttle);
            if (contract.assignedShuttle != shuttle || contract.state != TransportContractState.Assigned)
            {
                Cancel(contract);
                return null;
            }
            return contract;
        }

        private TransportContract CreateFreightContractInternal(
            int demandId,
            ResourceType resource, float quantity,
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
                priority = priority,
                sourceLocation = sourceLocation,
                destinationLocation = destinationLocation,
                state = TransportContractState.Open,
                creationTime = SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f,
                demandId = demandId,
                resourceType = resource,
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
            List<ColonistAgent> passengers, int priority = 100)
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
                priority = priority,
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

        public void Assign(TransportContract contract, ShuttleController shuttle)
        {
            if (contract == null || shuttle == null ||
                contract.state != TransportContractState.Open ||
                !shuttle.IsAvailable)
                return;

            if (contract.type == TransportContractType.Freight &&
                contract.quantity > shuttle.GetFreeCargoCapacity(contract.resourceType) + 0.0001f)
                return;

            contract.state = TransportContractState.Assigned;
            contract.assignedShuttle = shuttle;
            SimulationLog.Log($"Contract #{contract.contractId} assigned to {shuttle.displayName}");
            shuttle.StartContract(contract);
        }

        public void Complete(TransportContract contract)
        {
            if (contract == null)
                return;
            contract.state = TransportContractState.Completed;
            SimulationLog.Log($"Contract #{contract.contractId} completed");
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
                contract.sourceInventory.ReleaseReservation(contract.resourceType, stillReserved);
            }

            contract.state = TransportContractState.Cancelled;
            SimulationLog.Log($"Contract #{contract.contractId} cancelled");
        }
    }
}
