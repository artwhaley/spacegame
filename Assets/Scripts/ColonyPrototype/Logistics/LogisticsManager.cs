using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Chooses ordinary passenger and freight work through one deterministic
    /// arbitration path. Freight is materialized only after a vehicle has won.
    /// </summary>
    public class LogisticsManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public static LogisticsManager Instance { get; private set; }

        [SerializeField] private List<TransportVehicleComponent> transportVehicles =
            new List<TransportVehicleComponent>();
        [SerializeField] private List<FreightDemand> demands = new List<FreightDemand>();
        [SerializeField] private List<FreightSupply> supplies = new List<FreightSupply>();

        public IReadOnlyList<TransportVehicleComponent> TransportVehicles => transportVehicles;
        public IReadOnlyList<FreightDemand> Demands => demands;
        public IReadOnlyList<FreightSupply> Supplies => supplies;
        public int SimulationTickPriority => 300;

        private bool startupReported;
        private string lastWaitReason;
        private int nextDemandId = 1;
        private int nextSupplyId = 1;

        private const float QuantityEpsilon = 0.0001f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active LogisticsManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
            ReconcileIds();
            DiscoverTransportVehicles();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
            SimulationManager.RegisterTickable(this);
            ReconcileIds();
            DiscoverTransportVehicles();
            SimulationLog.Log("LogisticsManager online");
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (Instance == this)
                Instance = null;
        }

        private void DiscoverTransportVehicles()
        {
            PruneTransportVehicles();
            TransportVehicleComponent[] vehicles = FindObjectsByType<TransportVehicleComponent>(
                FindObjectsInactive.Include);
            for (int i = 0; i < vehicles.Length; i++)
                RegisterTransportVehicle(vehicles[i]);
        }

        private void PruneTransportVehicles()
        {
            for (int i = transportVehicles.Count - 1; i >= 0; i--)
                if (transportVehicles[i] == null)
                    transportVehicles.RemoveAt(i);
        }

        public void RegisterTransportVehicle(TransportVehicleComponent vehicle)
        {
            if (vehicle != null && !transportVehicles.Contains(vehicle))
                transportVehicles.Add(vehicle);
        }

        public void UnregisterTransportVehicle(TransportVehicleComponent vehicle)
        {
            if (vehicle != null)
                transportVehicles.Remove(vehicle);
        }

        public int RegisterFreightDemand(
            string displayName,
            ResourceDefinition resource,
            LocationAnchor destinationLocation,
            InventoryComponent destinationInventory,
            int priority,
            float minimumShipment,
            float maximumShipment,
            FreightDemandClass demandClass = FreightDemandClass.Foreground)
        {
            if (destinationLocation == null || destinationInventory == null || resource == null)
                return 0;

            FreightDemand demand = new FreightDemand
            {
                demandId = nextDemandId++,
                displayName = displayName,
                resource = resource,
                destinationLocation = destinationLocation,
                destinationInventory = destinationInventory,
                priority = TransportPriorityRules.Clamp(priority),
                minimumShipment = Mathf.Max(0f, minimumShipment),
                maximumShipment = Mathf.Max(0f, maximumShipment),
                demandClass = demandClass,
                active = false
            };
            demands.Add(demand);
            return demand.demandId;
        }

        public void UpdateFreightDemand(int demandId, float desiredQuantity)
        {
            FreightDemand demand = FindDemand(demandId);
            if (demand == null)
                return;

            long tick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick
                : 0L;
            demand.Update(desiredQuantity, tick);
        }

        public void UpdateFreightDemandPolicy(
            int demandId, int priority, float minimumShipment, float maximumShipment,
            FreightDemandClass demandClass)
        {
            FreightDemand demand = FindDemand(demandId);
            if (demand == null)
                return;

            demand.priority = TransportPriorityRules.Clamp(priority);
            demand.minimumShipment = Mathf.Max(0f, minimumShipment);
            demand.maximumShipment = Mathf.Max(0f, maximumShipment);
            demand.demandClass = demandClass;
        }

        public void UnregisterFreightDemand(int demandId)
        {
            for (int i = demands.Count - 1; i >= 0; i--)
                if (demands[i] != null && demands[i].demandId == demandId)
                {
                    demands.RemoveAt(i);
                    return;
                }
        }

        public int RegisterFreightSupply(
            string displayName,
            ResourceDefinition resource,
            LocationAnchor location,
            InventoryComponent inventory,
            float retainStock = 0f)
        {
            if (location == null || inventory == null || resource == null)
                return 0;

            for (int i = 0; i < supplies.Count; i++)
            {
                FreightSupply existing = supplies[i];
                if (existing != null && existing.location == location &&
                    existing.inventory == inventory && existing.resource == resource)
                {
                    existing.retainStock = Mathf.Max(0f, retainStock);
                    existing.active = true;
                    return existing.supplyId;
                }
            }

            FreightSupply supply = new FreightSupply
            {
                supplyId = nextSupplyId++,
                displayName = displayName,
                resource = resource,
                location = location,
                inventory = inventory,
                retainStock = Mathf.Max(0f, retainStock),
                active = true
            };
            supplies.Add(supply);
            return supply.supplyId;
        }

        public void UnregisterFreightSupply(int supplyId)
        {
            for (int i = supplies.Count - 1; i >= 0; i--)
                if (supplies[i] != null && supplies[i].supplyId == supplyId)
                {
                    supplies.RemoveAt(i);
                    return;
                }
        }

        public float GetExportableQuantity(FreightSupply supply)
        {
            if (supply == null || !supply.active || supply.inventory == null || supply.resource == null)
                return 0f;
            return Mathf.Max(0f, supply.inventory.GetAvailable(supply.resource) - supply.retainStock);
        }

        public void SimulationTick(float deltaGameHours)
        {
            PruneTransportVehicles();
            UpdatePlanningStates();
            TryAssignNext(reportBlocked: true);

            if (!startupReported)
            {
                startupReported = true;
                bool hasOpen = ContractManager.Instance != null &&
                    ContractManager.Instance.FindBestOpenContract() != null;
                SimulationLog.Log($"LogisticsManager first tick: {transportVehicles.Count} transport vehicle(s) registered, open contract waiting: {hasOpen}");
            }
        }

        /// <summary>
        /// Offers every idle vehicle the best currently eligible candidate. A
        /// freight candidate becomes a contract only after it wins arbitration.
        /// </summary>
        public void TryAssignNext(bool reportBlocked = false)
        {
            if (ContractManager.Instance == null)
                return;

            bool assignedAny = false;
            for (int i = 0; i < transportVehicles.Count; i++)
            {
                TransportVehicleComponent vehicle = transportVehicles[i];
                if (vehicle == null || !vehicle.IsAvailable)
                    continue;

                TransportDispatchCandidate candidate = FindBestCandidate(vehicle);
                if (candidate == null)
                    continue;

                if (MaterializeAndAssign(candidate, vehicle))
                    assignedAny = true;
            }

            if (assignedAny)
            {
                lastWaitReason = null;
                return;
            }

            if (reportBlocked && HasOpenWork())
                ReportWaiting(DescribeUnavailableVehicles());
        }

        private TransportDispatchCandidate FindBestCandidate(TransportVehicleComponent vehicle)
        {
            TransportDispatchCandidate best = null;
            for (int i = 0; i < demands.Count; i++)
            {
                FreightDemand demand = demands[i];
                TransportDispatchCandidate candidate = BuildFreightCandidate(demand, vehicle);
                if (candidate != null && IsBetterCandidate(candidate, best, vehicle))
                    best = candidate;
            }

            if (ContractManager.Instance != null)
            {
                IReadOnlyList<TransportContract> contracts = ContractManager.Instance.Contracts;
                for (int i = 0; i < contracts.Count; i++)
                {
                    TransportContract contract = contracts[i];
                    TransportDispatchCandidate candidate = BuildPassengerCandidate(contract, vehicle);
                    if (candidate != null && IsBetterCandidate(candidate, best, vehicle))
                        best = candidate;
                }
            }
            return best;
        }

        private TransportDispatchCandidate BuildPassengerCandidate(
            TransportContract contract, TransportVehicleComponent vehicle)
        {
            if (contract == null || contract.type != TransportContractType.Passenger ||
                contract.state != TransportContractState.Open || !vehicle.personnelEnabled ||
                vehicle.disposition == TransportDisposition.FreightOnly ||
                vehicle.passengerCarrier == null || contract.sourceLocation == null ||
                contract.destinationLocation == null ||
                contract.passengers.Count > vehicle.PassengerCapacity ||
                (vehicle.ship != null && vehicle.ship.IsTraveling && !vehicle.ship.HasSafeDock))
                return null;

            for (int i = 0; i < contract.passengers.Count; i++)
                if (contract.passengers[i] == null || contract.passengers[i].currentLocation != contract.sourceLocation)
                    return null;

            string boardingReason;
            if (!vehicle.passengerCarrier.CanBoardPassengers(
                    contract.passengers, contract.sourceLocation, out boardingReason))
            {
                ReportInvalidPassengerCandidate(contract, vehicle, boardingReason);
                if (boardingReason == "manifest contains the carrier's responsible pilot")
                    ContractManager.Instance.Cancel(contract);
                return null;
            }

            return new TransportDispatchCandidate
            {
                passengerContract = contract,
                priority = TransportPriorityRules.Clamp(contract.priority),
                demandClass = FreightDemandClass.Foreground,
                age = contract.creationTime,
                stableId = contract.contractId
            };
        }

        private void ReportInvalidPassengerCandidate(
            TransportContract contract, TransportVehicleComponent vehicle, string reason)
        {
            string diagnostic = $"contract #{contract.contractId} rejected by {vehicle.DisplayName}: {reason}";
            if (lastWaitReason != diagnostic)
            {
                lastWaitReason = diagnostic;
                SimulationLog.Log($"LogisticsManager: {diagnostic}");
            }
        }

        private TransportDispatchCandidate BuildFreightCandidate(
            FreightDemand demand, TransportVehicleComponent vehicle)
        {
            if (demand == null || !demand.active || demand.resource == null ||
                demand.destinationLocation == null || demand.destinationInventory == null ||
                !vehicle.freightEnabled || vehicle.disposition == TransportDisposition.PersonnelOnly ||
                (vehicle.ship != null && vehicle.ship.IsTraveling && !vehicle.ship.HasSafeDock))
                return null;

            float inbound = ContractManager.Instance.GetActiveFreightQuantityForDemand(demand.demandId);
            float uncovered = Mathf.Max(0f, demand.DesiredQuantity - inbound);
            float destinationFree = Mathf.Max(0f,
                demand.destinationInventory.GetFreeCapacity(demand.resource) - inbound);
            FreightSupply supply = FindBestSupply(demand.resource);
            if (uncovered <= QuantityEpsilon || destinationFree <= QuantityEpsilon || supply == null)
                return null;

            float maximum = demand.maximumShipment > QuantityEpsilon
                ? demand.maximumShipment
                : float.MaxValue;
            float quantity = Mathf.Min(uncovered,
                Mathf.Min(destinationFree,
                    Mathf.Min(GetExportableQuantity(supply),
                        Mathf.Min(vehicle.GetFreeCargoCapacity(demand.resource), maximum))));
            if (demand.resource.IsDiscrete)
                quantity = Mathf.Floor(quantity + ResourceQuantityRules.WholeNumberEpsilon);
            if (quantity < demand.minimumShipment - QuantityEpsilon)
                return null;

            return new TransportDispatchCandidate
            {
                freightDemand = demand,
                freightSupply = supply,
                legalQuantity = quantity,
                priority = TransportPriorityRules.Clamp(demand.priority),
                demandClass = demand.demandClass,
                age = demand.ActiveSinceGameHour,
                stableId = demand.demandId
            };
        }

        private bool IsBetterCandidate(
            TransportDispatchCandidate candidate,
            TransportDispatchCandidate current,
            TransportVehicleComponent vehicle)
        {
            if (current == null)
                return true;

            if (candidate.demandClass != current.demandClass)
                return candidate.demandClass == FreightDemandClass.Foreground;
            if (candidate.priority != current.priority)
                return candidate.priority > current.priority;

            int candidatePreference = PreferenceRank(candidate, vehicle.disposition);
            int currentPreference = PreferenceRank(current, vehicle.disposition);
            if (candidatePreference != currentPreference)
                return candidatePreference > currentPreference;
            if (!Mathf.Approximately((float)candidate.age, (float)current.age))
                return candidate.age < current.age;
            return candidate.stableId < current.stableId;
        }

        private static int PreferenceRank(
            TransportDispatchCandidate candidate, TransportDisposition disposition)
        {
            if (disposition == TransportDisposition.PreferFreight)
                return candidate.IsFreight ? 1 : 0;
            if (disposition == TransportDisposition.PreferPersonnel)
                return candidate.IsFreight ? 0 : 1;
            return 0;
        }

        private bool MaterializeAndAssign(
            TransportDispatchCandidate candidate, TransportVehicleComponent vehicle)
        {
            TransportContract contract = candidate.passengerContract;
            if (candidate.IsFreight)
            {
                FreightDemand demand = candidate.freightDemand;
                contract = ContractManager.Instance.CreateDemandFreightContract(
                    demand.demandId, demand.resource, candidate.legalQuantity,
                    candidate.freightSupply.inventory, demand.destinationInventory,
                    candidate.freightSupply.location, demand.destinationLocation,
                    demand.priority);
                if (contract == null || contract.quantity < demand.minimumShipment - QuantityEpsilon)
                {
                    if (contract != null)
                        ContractManager.Instance.Cancel(contract);
                    return false;
                }
            }

            if (!ContractManager.Instance.Assign(contract, vehicle))
            {
                if (candidate.IsFreight)
                    ContractManager.Instance.Cancel(contract);
                return false;
            }
            return true;
        }

        private void UpdatePlanningStates()
        {
            for (int i = 0; i < demands.Count; i++)
            {
                FreightDemand demand = demands[i];
                if (demand == null || demand.destinationInventory == null)
                    continue;

                float inbound = ContractManager.Instance != null
                    ? ContractManager.Instance.GetActiveFreightQuantityForDemand(demand.demandId)
                    : 0f;
                float uncovered = Mathf.Max(0f, demand.DesiredQuantity - inbound);
                float destinationFree = Mathf.Max(0f,
                    demand.destinationInventory.GetFreeCapacity(demand.resource) - inbound);
                string status = !demand.active || uncovered <= QuantityEpsilon
                    ? "Satisfied"
                    : destinationFree <= QuantityEpsilon
                        ? "Waiting for destination capacity"
                        : FindBestSupply(demand.resource) == null
                            ? "Waiting for source"
                            : "Waiting for transport";
                if (inbound > QuantityEpsilon && IsInboundStalled(demand.demandId))
                    status = "Stalled: inbound transport unavailable";
                demand.SetPlanningState(inbound, uncovered, destinationFree, status);
            }
        }

        private FreightDemand FindDemand(int demandId)
        {
            for (int i = 0; i < demands.Count; i++)
                if (demands[i] != null && demands[i].demandId == demandId)
                    return demands[i];
            return null;
        }

        private FreightSupply FindBestSupply(ResourceDefinition resource)
        {
            FreightSupply best = null;
            float bestAvailable = 0f;
            for (int i = 0; i < supplies.Count; i++)
            {
                FreightSupply candidate = supplies[i];
                if (candidate == null || !candidate.active || candidate.location == null ||
                    candidate.inventory == null || candidate.resource != resource)
                    continue;

                float available = GetExportableQuantity(candidate);
                if (available <= QuantityEpsilon)
                    continue;
                if (best == null || available > bestAvailable)
                {
                    best = candidate;
                    bestAvailable = available;
                }
            }
            return best;
        }

        private bool HasOpenWork()
        {
            if (ContractManager.Instance != null)
            {
                IReadOnlyList<TransportContract> contracts = ContractManager.Instance.Contracts;
                for (int i = 0; i < contracts.Count; i++)
                    if (contracts[i] != null && contracts[i].state == TransportContractState.Open)
                        return true;
            }
            for (int i = 0; i < demands.Count; i++)
                if (demands[i] != null && demands[i].active && demands[i].UncoveredQuantity > QuantityEpsilon)
                    return true;
            return false;
        }

        private void ReportWaiting(string reason)
        {
            if (lastWaitReason == reason)
                return;
            lastWaitReason = reason;
            SimulationLog.Log($"LogisticsManager: work waiting - {reason}");
        }

        private string DescribeUnavailableVehicles()
        {
            if (transportVehicles.Count == 0)
                return "no transport vehicles registered";

            for (int i = 0; i < transportVehicles.Count; i++)
                if (transportVehicles[i] != null && transportVehicles[i].IsAvailable)
                    return "vehicle available, but no eligible candidate";

            string description = transportVehicles.Count == 1
                ? "1 vehicle, none available"
                : $"{transportVehicles.Count} vehicles, none available";
            for (int i = 0; i < transportVehicles.Count; i++)
            {
                TransportVehicleComponent vehicle = transportVehicles[i];
                description += vehicle == null
                    ? " [<null vehicle>]"
                    : $" [{vehicle.DisplayName}: {GetAvailabilityBlocker(vehicle)}]";
            }
            return description;
        }

        public bool TryGetVehicleAvailability(TransportVehicleComponent vehicle, out VehicleAvailability availability)
        {
            if (vehicle == null)
            {
                availability = new VehicleAvailability(false, VehicleAvailabilityReason.MissingShip, "vehicle missing");
                return false;
            }
            return vehicle.TryGetAvailability(out availability);
        }

        private static string GetAvailabilityBlocker(TransportVehicleComponent vehicle)
        {
            vehicle.TryGetAvailability(out VehicleAvailability availability);
            return availability.Blocker;
        }

        private bool IsInboundStalled(int demandId)
        {
            if (ContractManager.Instance == null)
                return false;
            IReadOnlyList<TransportContract> contracts = ContractManager.Instance.Contracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                TransportContract contract = contracts[i];
                if (contract == null || contract.demandId != demandId || !contract.IsActive ||
                    contract.assignedVehicle == null)
                    continue;
                TransportExecutorComponent executor = contract.assignedVehicle.GetComponent<TransportExecutorComponent>();
                if (!contract.assignedVehicle.isActiveAndEnabled || executor == null || !executor.isActiveAndEnabled)
                    return true;
            }
            return false;
        }

        private void ReconcileIds()
        {
            for (int i = 0; i < demands.Count; i++)
                if (demands[i] != null)
                    nextDemandId = Mathf.Max(nextDemandId, demands[i].demandId + 1);
            for (int i = 0; i < supplies.Count; i++)
                if (supplies[i] != null)
                    nextSupplyId = Mathf.Max(nextSupplyId, supplies[i].supplyId + 1);
        }
    }
}
