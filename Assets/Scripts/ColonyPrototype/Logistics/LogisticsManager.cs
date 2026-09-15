using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Publishes and reconciles freight demand, then assigns the highest-priority
    /// open legacy contract to an idle available shuttle. One shuttle exists, so
    /// no fleet optimization is attempted.
    ///
    /// Demand reconciliation creates a contract only after source stock and an idle
    /// compatible shuttle have been found. Legacy callers may still create open
    /// contracts, which are assigned immediately or from the normal tick.
    /// </summary>
    public class LogisticsManager : MonoBehaviour, ISimulationTickable
    {
        public static LogisticsManager Instance { get; private set; }

        [SerializeField] private List<ShuttleController> shuttles = new List<ShuttleController>();
        [SerializeField] private List<FreightDemand> demands = new List<FreightDemand>();
        [SerializeField] private List<FreightSupply> supplies = new List<FreightSupply>();

        public IReadOnlyList<ShuttleController> Shuttles => shuttles;
        public IReadOnlyList<FreightDemand> Demands => demands;
        public IReadOnlyList<FreightSupply> Supplies => supplies;

        private bool startupReported;
        private string lastWaitReason;
        private int nextDemandId = 1;
        private int nextSupplyId = 1;

        private const float QuantityEpsilon = 0.0001f;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
            SimulationLog.Log("LogisticsManager online");
        }

        private void OnDisable()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        public void RegisterShuttle(ShuttleController shuttle)
        {
            if (shuttle != null && !shuttles.Contains(shuttle))
                shuttles.Add(shuttle);
        }

        public void UnregisterShuttle(ShuttleController shuttle)
        {
            if (shuttle != null)
                shuttles.Remove(shuttle);
        }

        /// <summary>
        /// Registers one replaceable freight demand. The returned ID is the stable
        /// identity a consumer uses when publishing new demand snapshots.
        /// </summary>
        public int RegisterFreightDemand(
            string displayName,
            ResourceDefinition resource,
            LocationAnchor destinationLocation,
            InventoryComponent destinationInventory,
            int priority,
            float minimumShipment,
            float maximumShipment)
        {
            if (destinationLocation == null || destinationInventory == null)
                return 0;

            FreightDemand demand = new FreightDemand
            {
                demandId = nextDemandId++,
                displayName = displayName,
                resource = resource,
                destinationLocation = destinationLocation,
                destinationInventory = destinationInventory,
                priority = priority,
                minimumShipment = Mathf.Max(0f, minimumShipment),
                maximumShipment = Mathf.Max(0f, maximumShipment),
                active = false
            };
            demands.Add(demand);
            return demand.demandId;
        }

        /// <summary>Replaces the current desired delivery quantity for a demand.</summary>
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

        public void UnregisterFreightDemand(int demandId)
        {
            for (int i = demands.Count - 1; i >= 0; i--)
            {
                if (demands[i] != null && demands[i].demandId == demandId)
                {
                    demands.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Registers a live source for one resource. The source inventory remains
        /// authoritative for on-hand, reserved, and available quantities.
        /// </summary>
        public int RegisterFreightSupply(
            string displayName,
            ResourceDefinition resource,
            LocationAnchor location,
            InventoryComponent inventory)
        {
            if (location == null || inventory == null)
                return 0;

            for (int i = 0; i < supplies.Count; i++)
            {
                FreightSupply existing = supplies[i];
                if (existing != null && existing.location == location &&
                    existing.inventory == inventory && existing.resource == resource)
                {
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
                active = true
            };
            supplies.Add(supply);
            return supply.supplyId;
        }

        public void UnregisterFreightSupply(int supplyId)
        {
            for (int i = supplies.Count - 1; i >= 0; i--)
            {
                if (supplies[i] != null && supplies[i].supplyId == supplyId)
                {
                    supplies.RemoveAt(i);
                    return;
                }
            }
        }

        public void SimulationTick(float deltaGameHours)
        {
            ReconcileFreightDemands();

            if (!startupReported)
            {
                startupReported = true;
                bool hasOpen = ContractManager.Instance != null &&
                               ContractManager.Instance.FindBestOpenContract() != null;
                SimulationLog.Log($"LogisticsManager first tick: {shuttles.Count} shuttle(s) registered, open contract waiting: {hasOpen}");
            }

            TryAssignNext(reportBlocked: true);
        }

        /// <summary>
        /// Converts the latest demand snapshots into at most one new delivery per
        /// demand per simulation tick. Demand is reconciled against remaining active
        /// inbound quantities, source reservations, destination capacity, and a
        /// selected idle shuttle before a contract is created.
        /// </summary>
        private void ReconcileFreightDemands()
        {
            if (ContractManager.Instance == null)
                return;

            HashSet<int> processed = new HashSet<int>();
            while (processed.Count < demands.Count)
            {
                FreightDemand demand = FindHighestPriorityUnprocessedDemand(processed);
                if (demand == null)
                    break;

                processed.Add(demand.demandId);
                ReconcileDemand(demand);
            }
        }

        private FreightDemand FindHighestPriorityUnprocessedDemand(HashSet<int> processed)
        {
            FreightDemand best = null;
            for (int i = 0; i < demands.Count; i++)
            {
                FreightDemand candidate = demands[i];
                if (candidate == null || processed.Contains(candidate.demandId))
                    continue;

                if (best == null || candidate.priority > best.priority ||
                    (candidate.priority == best.priority && candidate.demandId < best.demandId))
                {
                    best = candidate;
                }
            }
            return best;
        }

        private void ReconcileDemand(FreightDemand demand)
        {
            if (demand == null)
                return;

            float inbound = ContractManager.Instance.GetActiveFreightQuantityForDemand(demand.demandId);
            if (!demand.active || demand.destinationLocation == null || demand.destinationInventory == null)
            {
                demand.SetPlanningState(inbound, 0f, 0f, "Satisfied");
                return;
            }

            float uncovered = Mathf.Max(0f, demand.DesiredQuantity - inbound);
            float destinationFree = Mathf.Max(
                0f,
                demand.destinationInventory.GetFreeCapacity(demand.resource) - inbound);
            demand.SetPlanningState(inbound, uncovered, destinationFree, "Evaluating");

            if (uncovered <= QuantityEpsilon)
            {
                demand.SetPlanningState(inbound, 0f, destinationFree, "Inbound covers demand");
                return;
            }

            float minimumShipment = Mathf.Max(0f, demand.minimumShipment);
            if (destinationFree <= QuantityEpsilon)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for destination capacity");
                return;
            }

            FreightSupply supply = FindBestSupply(demand.resource);
            if (supply == null)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for source");
                return;
            }

            float sourceAvailable = supply.inventory.GetAvailable(demand.resource);
            if (sourceAvailable <= QuantityEpsilon)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for source stock");
                return;
            }

            ShuttleController shuttle = FindAvailableShuttle(demand.resource);
            if (shuttle == null)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for shuttle");
                return;
            }

            float maximumShipment = demand.maximumShipment > QuantityEpsilon
                ? demand.maximumShipment
                : float.MaxValue;
            float quantity = Mathf.Min(
                Mathf.Min(uncovered, destinationFree),
                Mathf.Min(sourceAvailable, Mathf.Min(shuttle.GetFreeCargoCapacity(demand.resource), maximumShipment)));

            if (quantity < minimumShipment - QuantityEpsilon)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for minimum shipment");
                return;
            }

            TransportContract contract = ContractManager.Instance.CreateAssignedFreightContract(
                demand.demandId,
                demand.resource,
                quantity,
                supply.inventory,
                demand.destinationInventory,
                supply.location,
                demand.destinationLocation,
                shuttle,
                demand.priority,
                minimumShipment);

            if (contract == null)
            {
                demand.SetPlanningState(inbound, uncovered, destinationFree, "Waiting for source or capacity");
                return;
            }

            float updatedInbound = ContractManager.Instance.GetActiveFreightQuantityForDemand(demand.demandId);
            demand.SetPlanningState(
                updatedInbound,
                Mathf.Max(0f, demand.DesiredQuantity - updatedInbound),
                Mathf.Max(0f, demand.destinationInventory.GetFreeCapacity(demand.resource) - updatedInbound),
                "Contract in transit");
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

                float available = candidate.inventory.GetAvailable(resource);
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

        /// <summary>
        /// Assigns the best waiting legacy contract to an idle shuttle, if both exist.
        /// Safe to call at any time. Only the tick passes reportBlocked: true, so brief
        /// startup moments (a legacy contract created before shuttles register) stay quiet.
        /// </summary>
        public void TryAssignNext(bool reportBlocked = false)
        {
            if (ContractManager.Instance == null)
                return;

            TransportContract contract = ContractManager.Instance.FindBestOpenContract();
            if (contract == null)
            {
                lastWaitReason = null;
                return;
            }

            ShuttleController shuttle = FindIdleShuttle();
            if (shuttle == null)
            {
                if (reportBlocked)
                    ReportWaiting(DescribeUnavailableShuttles());
                return;
            }

            lastWaitReason = null;
            ContractManager.Instance.Assign(contract, shuttle);
        }

        /// <summary>
        /// Logs why waiting work is not moving, but only when the reason changes, so a
        /// stuck shuttle is announced once instead of every tick. Without this, a
        /// permanently unavailable shuttle silently swallows the whole backlog.
        /// </summary>
        private void ReportWaiting(string reason)
        {
            if (lastWaitReason == reason)
                return;
            lastWaitReason = reason;
            SimulationLog.Log($"LogisticsManager: contract waiting - {reason}");
        }

        private string DescribeUnavailableShuttles()
        {
            if (shuttles.Count == 0)
                return "no shuttles registered";

            string description = shuttles.Count == 1
                ? "1 shuttle, none available"
                : $"{shuttles.Count} shuttles, none available";

            for (int i = 0; i < shuttles.Count; i++)
            {
                ShuttleController shuttle = shuttles[i];
                description += shuttle == null
                    ? " [<null shuttle>]"
                    : $" [{shuttle.displayName}: {shuttle.AvailabilityBlocker()}]";
            }
            return description;
        }

        private ShuttleController FindIdleShuttle()
        {
            for (int i = 0; i < shuttles.Count; i++)
                if (shuttles[i] != null && shuttles[i].IsAvailable)
                    return shuttles[i];
            return null;
        }

        private ShuttleController FindAvailableShuttle(ResourceDefinition resource)
        {
            for (int i = 0; i < shuttles.Count; i++)
            {
                ShuttleController shuttle = shuttles[i];
                if (shuttle != null && shuttle.IsAvailable &&
                    shuttle.GetFreeCargoCapacity(resource) > QuantityEpsilon)
                {
                    return shuttle;
                }
            }
            return null;
        }
    }
}
