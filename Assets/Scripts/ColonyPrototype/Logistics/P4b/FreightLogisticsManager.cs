using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum FreightJobState
    {
        Assigned,
        TravelingToPickup,
        PickingUp,
        TravelingToDropoff,
        DroppingOff,
        Completed,
        Cancelled,
        Blocked
    }

    public sealed class FreightOrder
    {
        internal FreightOrder(string id, LogisticsStockComponent requester,
            ResourceDefinition resource, float requested, long openedTick)
        {
            Id = id;
            Requester = requester;
            Resource = resource;
            Requested = requested;
            OpenedTick = openedTick;
            LastRefreshTick = openedTick;
            IsOpen = true;
        }

        public string Id { get; }
        public LogisticsStockComponent Requester { get; }
        public ResourceDefinition Resource { get; }
        public float Requested { get; }
        public float Delivered { get; internal set; }
        public float Committed { get; internal set; }
        public long OpenedTick { get; }
        public long LastRefreshTick { get; internal set; }
        public bool IsOpen { get; internal set; }
        public float Uncovered => Mathf.Max(0f, Requested - Delivered - Committed);
    }

    public sealed class FreightDeliveryJob
    {
        internal FreightDeliveryJob(string id, FreightOrder order,
            LogisticsStockComponent source, WalkingFreightCarrierComponent carrier,
            InventoryReservationToken reservation, float quantity, bool emergency,
            LogisticsRoutePlan routePlan)
        {
            Allocation = new FreightAllocation(id, order, source, carrier, quantity, routePlan);
            Reservation = reservation;
            IsEmergencyExcursion = emergency;
            State = FreightJobState.Assigned;
        }

        public FreightAllocation Allocation { get; }
        public LogisticsRoutePlan RoutePlan => Allocation.RoutePlan;
        public string Id => Allocation.Id;
        public FreightOrder Order => Allocation.Order;
        public ResourceDefinition Resource => Allocation.Resource;
        public LogisticsStockComponent Source => Allocation.Source;
        public LogisticsStockComponent Destination => Allocation.Destination;
        public WalkingFreightCarrierComponent Carrier => Allocation.Carrier;
        public InventoryReservationToken Reservation { get; }
        public float Quantity { get => Allocation.Quantity; internal set => Allocation.Quantity = value; }
        public bool IsEmergencyExcursion { get; }
        public FreightJobState State { get; internal set; }
        public bool HasPickedUp { get; internal set; }
        public bool IsTerminal => State == FreightJobState.Completed || State == FreightJobState.Cancelled;
        public long RetryAtTick { get; internal set; }
    }

    /// <summary>Owns local stock orders and walking freight allocations for P4b.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Freight Logistics Manager")]
    public sealed class FreightLogisticsManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float QuantityEpsilon = 0.0001f;
        private const long PublicationGraceTicks = 3;
        private const long BlockedRetryTicks = 5;
        private static long nextOrderId;
        private static long nextJobId;

        [SerializeField] private JobRoleDefinition routineCarrierRole;

        [NonSerialized] private readonly List<FreightOrder> orders = new List<FreightOrder>();
        [NonSerialized] private readonly List<FreightDeliveryJob> jobs = new List<FreightDeliveryJob>();

        public static FreightLogisticsManager Instance { get; private set; }
        public IReadOnlyList<FreightOrder> Orders => orders;
        public IReadOnlyList<FreightDeliveryJob> Jobs => jobs;
        public int SimulationTickPriority => 310;

        public JobRoleDefinition RoutineCarrierRole => routineCarrierRole;

        public void ConfigureRoutineCarrierRole(JobRoleDefinition role)
        {
            routineCarrierRole = role;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one FreightLogisticsManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            nextOrderId = 0L;
            nextJobId = 0L;
        }

        private void OnEnable() => SimulationManager.RegisterTickable(this);

        private void OnDisable() => SimulationManager.UnregisterTickable(this);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Publish(LogisticsStockComponent stock)
        {
            if (stock == null || stock.Inventory == null || stock.Policies == null)
                return;

            long tick = CurrentTick;
            for (int i = 0; i < stock.Policies.Count; i++)
            {
                LogisticsStockPolicyEntry policy = stock.Policies[i];
                if (policy == null || policy.resource == null ||
                    policy.role != LogisticsStockRole.Consumer)
                    continue;

                FreightOrder current = FindOpenOrder(stock, policy.resource);
                if (current != null)
                {
                    current.LastRefreshTick = tick;
                    continue;
                }

                float onHand = stock.Inventory.GetOnHand(policy.resource);
                if (onHand > policy.reorderThreshold + QuantityEpsilon)
                    continue;

                float requested = Mathf.Max(0f, policy.ResolveTarget(stock.Inventory) - onHand);
                if (requested <= QuantityEpsilon)
                    continue;

                FreightOrder order = new FreightOrder(
                    "food-order-" + (++nextOrderId).ToString("D6"),
                    stock,
                    policy.resource,
                    requested,
                    tick);
                orders.Add(order);
                Log("logistics.demand_opened", "Info", stock, null,
                    new SimulationLogField("demandId", order.Id),
                    new SimulationLogField("resource", policy.resource.name),
                    new SimulationLogField("requested", requested));
            }
        }

        public void SimulationTick(float deltaGameHours)
        {
            long tick = CurrentTick;
            ExpireUnpublishedOrders(tick);
            CloseFulfilledOrders();
            DispatchEmergencyExcursions();
            DispatchRoutineJobs();
        }

        public void SetJobState(FreightDeliveryJob job, FreightJobState state, string reason = null)
        {
            if (!IsKnown(job) || job.State == state)
                return;
            job.State = state;
            Log(state == FreightJobState.Blocked ? "logistics.job_blocked" : "logistics.job_state_changed",
                state == FreightJobState.Blocked ? "Warning" : "Info",
                job.Carrier, job.Destination,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("demandId", job.Order != null ? job.Order.Id : string.Empty),
                new SimulationLogField("state", state),
                new SimulationLogField("reason", reason ?? string.Empty),
                new SimulationLogField("quantity", job.Quantity),
                new SimulationLogField("routeDistance", job.RoutePlan.TotalEstimatedDistance),
                new SimulationLogField("freightLeg", state == FreightJobState.TravelingToPickup ||
                    state == FreightJobState.PickingUp ? "pickup" :
                    state == FreightJobState.TravelingToDropoff || state == FreightJobState.DroppingOff
                        ? "delivery" : "none"),
                new SimulationLogField("reservationActive", job.Reservation != null && job.Reservation.IsActive));
        }

        public bool TryPickup(FreightDeliveryJob job)
        {
            if (!IsKnown(job) || job.HasPickedUp || job.IsTerminal ||
                job.Source == null || job.Source.Inventory == null ||
                job.Destination == null || job.Destination.Inventory == null ||
                job.Reservation == null || !job.Reservation.IsActive)
            {
                CancelBeforePickup(job, "pickup_precondition_failed");
                return false;
            }

            FreightOrder order = job.Order;
            float otherInbound = GetCommittedCapacity(job.Destination.Inventory,
                job.Resource, job);
            float availableDestinationCapacity = Mathf.Max(0f,
                job.Destination.Inventory.GetFreeCapacity(job.Resource) - otherInbound);
            if (availableDestinationCapacity + QuantityEpsilon < job.Quantity ||
                job.Reservation.Remaining + QuantityEpsilon < job.Quantity)
            {
                CancelBeforePickup(job, "allocation_no_longer_fits");
                return false;
            }

            job.State = FreightJobState.PickingUp;
            if (job.Carrier == null || job.Carrier.CargoInventory == null)
            {
                CancelBeforePickup(job, "carrier_inventory_missing");
                return false;
            }
            float moved = job.Source.Inventory.TransferOwnedTo(
                job.Reservation,
                job.Carrier.CargoInventory,
                job.Quantity);
            if (moved <= QuantityEpsilon)
            {
                CancelBeforePickup(job, "reserved_stock_unavailable");
                return false;
            }

            float reduced = Mathf.Max(0f, job.Quantity - moved);
            order.Committed = Mathf.Max(0f, order.Committed - reduced);
            job.Quantity = moved;
            job.HasPickedUp = true;
            if (job.IsEmergencyExcursion && job.Carrier.Brain != null)
                job.Carrier.Brain.SetWorkExcursionCargo(true);

            Log("logistics.pickup_completed", "Info", job.Carrier, job.Source,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("quantity", moved),
                new SimulationLogField("demandId", order.Id));
            return true;
        }

        public bool TryDeliver(FreightDeliveryJob job)
        {
            if (!IsKnown(job) || !job.HasPickedUp || job.IsTerminal ||
                job.Destination == null || job.Destination.Inventory == null ||
                job.Carrier == null || job.Carrier.CargoInventory == null)
            {
                BlockJob(job, "destination_or_carrier_unavailable");
                return false;
            }

            float orderRemainder = Mathf.Max(0f, job.Order.Requested - job.Order.Delivered);
            float requestedTransfer = Mathf.Min(job.Quantity, orderRemainder);
            if (requestedTransfer <= QuantityEpsilon)
            {
                BlockJob(job, "order_fulfilled_carrier_retains_excess_cargo");
                return false;
            }

            job.State = FreightJobState.DroppingOff;
            float moved = job.Carrier.CargoInventory.TransferAvailableTo(
                job.Destination.Inventory,
                job.Resource,
                requestedTransfer);
            if (moved <= QuantityEpsilon)
            {
                BlockJob(job, "destination_has_no_capacity");
                return false;
            }

            job.Quantity = Mathf.Max(0f, job.Quantity - moved);
            job.Order.Committed = Mathf.Max(0f, job.Order.Committed - moved);
            job.Order.Delivered += moved;
            Log("logistics.delivery_completed", "Info", job.Carrier, job.Destination,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("quantity", moved),
                new SimulationLogField("demandId", job.Order.Id));

            if (job.Quantity > QuantityEpsilon)
            {
                BlockJob(job, "carrier_retains_unaccepted_cargo");
                return false;
            }

            job.State = FreightJobState.Completed;
            CloseFulfilledOrders();
            FinishCarrier(job);
            return true;
        }

        public void CancelBeforePickup(FreightDeliveryJob job, string reason)
        {
            if (!IsKnown(job) || job.HasPickedUp || job.IsTerminal)
                return;

            job.Source?.Inventory?.ReleaseOwned(job.Reservation);
            if (job.Order != null)
                job.Order.Committed = Mathf.Max(0f, job.Order.Committed - job.Quantity);
            job.State = FreightJobState.Cancelled;
            Log("logistics.job_cancelled", "Warning", job.Carrier, job.Destination,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("demandId", job.Order != null ? job.Order.Id : string.Empty),
                new SimulationLogField("reason", reason ?? string.Empty),
                new SimulationLogField("quantity", job.Quantity));
            FinishCarrier(job);
        }

        public void BlockJob(FreightDeliveryJob job, string reason)
        {
            if (!IsKnown(job) || job.IsTerminal)
                return;
            job.RetryAtTick = reason == "order_fulfilled_carrier_retains_excess_cargo"
                ? long.MaxValue
                : CurrentTick + BlockedRetryTicks;
            SetJobState(job, FreightJobState.Blocked, reason);
        }

        public void ResumeJob(FreightDeliveryJob job, FreightJobState state)
        {
            if (!IsKnown(job) || job.IsTerminal)
                return;
            job.RetryAtTick = 0L;
            SetJobState(job, state, "retry_started");
        }

        private void DispatchRoutineJobs()
        {
            List<FreightOrder> pending = GetDispatchOrderPriority();
            for (int i = 0; i < pending.Count; i++)
            {
                FreightOrder order = pending[i];
                float dispatchable = GetDispatchableQuantity(order);
                if (dispatchable + QuantityEpsilon < GetMinimumPickup(order))
                    continue;

                Candidate best = FindBestCandidate(order, dispatchable, false);
                if (best == null)
                    continue;
                AcceptCandidate(order, best, false);
            }
        }

        private void DispatchEmergencyExcursions()
        {
            List<FreightOrder> pending = GetDispatchOrderPriority();
            for (int i = 0; i < pending.Count; i++)
            {
                FreightOrder order = pending[i];
                if (order.Requester == null || order.Requester.Inventory == null ||
                    !order.Requester.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry policy) ||
                    policy.role != LogisticsStockRole.Consumer ||
                    order.Requester.Inventory.GetOnHand(order.Resource) > policy.emergencyThreshold + QuantityEpsilon)
                {
                    continue;
                }

                float actionableIncoming = GetActionableIncoming(order.Requester, order.Resource);
                if (order.Requester.Inventory.GetOnHand(order.Resource) + actionableIncoming + QuantityEpsilon >=
                    policy.reorderThreshold)
                {
                    continue;
                }

                float dispatchable = GetDispatchableQuantity(order);
                if (dispatchable + QuantityEpsilon < policy.minimumPickup)
                    continue;

                Candidate best = FindBestCandidate(order, dispatchable, true);
                if (best != null)
                    AcceptCandidate(order, best, true);
            }
        }

        private Candidate FindBestCandidate(FreightOrder order, float dispatchable, bool emergencyOnly)
        {
            Candidate best = null;
            IReadOnlyList<WalkingFreightCarrierComponent> carriers = WalkingFreightCarrierComponent.Active;
            IReadOnlyList<LogisticsStockComponent> supplies = LogisticsStockComponent.Active;
            for (int c = 0; c < carriers.Count; c++)
            {
                WalkingFreightCarrierComponent carrier = carriers[c];
                if (carrier == null || (emergencyOnly
                        ? !carrier.CanAcceptEmergencyJob(order.Requester.GetComponent<WorkplaceComponent>())
                        : !carrier.CanAcceptRoutineJob(routineCarrierRole, out _)))
                    continue;

                float cargoCapacity = Mathf.Max(0f,
                    carrier.MaximumCargoQuantity - carrier.CargoInventory.GetOnHand(order.Resource));
                for (int s = 0; s < supplies.Count; s++)
                {
                    LogisticsStockComponent source = supplies[s];
                    if (source == null || source == order.Requester || source.Inventory == null ||
                        !source.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry supplyPolicy) ||
                        supplyPolicy.role == LogisticsStockRole.Consumer)
                        continue;

                    float destinationSpace = order.Requester.Inventory.GetFreeCapacity(order.Resource) -
                        GetCommittedCapacity(order.Requester.Inventory, order.Resource, null);
                    float quantity = Mathf.Min(dispatchable,
                        Mathf.Min(cargoCapacity,
                            Mathf.Min(source.Inventory.GetAvailable(order.Resource), destinationSpace)));
                    if (quantity + QuantityEpsilon < GetMinimumPickup(order))
                        continue;

                    PersonnelRoutingManager personnelRouting = PersonnelRoutingManager.Instance;
                    if (personnelRouting == null || carrier.Identity == null ||
                        source.FreightAnchor == null || order.Requester.FreightAnchor == null ||
                        !personnelRouting.TryEstimate(carrier.Identity, source.FreightAnchor,
                            out PersonnelRouteEstimate pickupEstimate) ||
                        !personnelRouting.TryEstimateFrom(carrier.Identity, source.FreightAnchor.position,
                            order.Requester.FreightAnchor, out PersonnelRouteEstimate deliveryEstimate))
                        continue;

                    Candidate candidate = new Candidate(carrier, source, quantity,
                        pickupEstimate, deliveryEstimate,
                        PersonnelRouteIdentity.GetStableKey(carrier.Identity) + "/" +
                        source.GetStableKey() + "/" + order.Id);
                    if (best == null || Candidate.Compare(candidate, best) < 0)
                        best = candidate;
                }
            }
            return best;
        }

        private void AcceptCandidate(FreightOrder order, Candidate candidate, bool emergency)
        {
            bool excursionStarted = false;
            if (emergency)
            {
                excursionStarted = candidate.Carrier.BeginEmergencyExcursion(
                    order.Requester.GetComponent<WorkplaceComponent>());
                if (!excursionStarted)
                    return;
            }

            if (!candidate.Source.Inventory.TryReserveOwned(
                    order.Resource, candidate.Quantity, out InventoryReservationToken reservation))
            {
                if (excursionStarted)
                    candidate.Carrier.Brain.CompleteWorkExcursion();
                return;
            }

            string jobId = "freight-job-" + (++nextJobId).ToString("D6");
            Transform sourceAnchor = candidate.Source.FreightAnchor;
            Transform destinationAnchor = order.Requester.FreightAnchor;
            LogisticsRoutePlan routePlan = new LogisticsRoutePlan(order, candidate.Carrier,
                new[]
                {
                    new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                        candidate.PickupEstimate.StartPosition, sourceAnchor,
                        candidate.PickupEstimate.Distance),
                    new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                        candidate.DeliveryEstimate.StartPosition, destinationAnchor,
                        candidate.DeliveryEstimate.Distance)
                });
            FreightDeliveryJob job = new FreightDeliveryJob(
                jobId, order,
                candidate.Source, candidate.Carrier,
                reservation, candidate.Quantity, emergency, routePlan);
            if (candidate.Carrier.CargoInventory == null ||
                !candidate.Carrier.CargoInventory.SetCapacity(
                    order.Resource, candidate.Carrier.MaximumCargoQuantity))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                if (excursionStarted)
                    candidate.Carrier.Brain.CompleteWorkExcursion();
                return;
            }
            order.Committed += candidate.Quantity;
            jobs.Add(job);
            if (!candidate.Carrier.Assign(job))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                order.Committed = Mathf.Max(0f, order.Committed - candidate.Quantity);
                job.State = FreightJobState.Cancelled;
                if (excursionStarted)
                    candidate.Carrier.Brain.CompleteWorkExcursion();
                return;
            }

            Log("logistics.job_assigned", "Info", candidate.Carrier, order.Requester,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("demandId", order.Id),
                new SimulationLogField("resource", order.Resource.name),
                new SimulationLogField("quantity", candidate.Quantity),
                new SimulationLogField("source", candidate.Source.name),
                new SimulationLogField("distance", candidate.Distance),
                new SimulationLogField("pickupAnchor", sourceAnchor.name),
                new SimulationLogField("pickupDistance", candidate.PickupEstimate.Distance),
                new SimulationLogField("dropoffAnchor", destinationAnchor.name),
                new SimulationLogField("dropoffDistance", candidate.DeliveryEstimate.Distance),
                new SimulationLogField("routeLegs", job.RoutePlan.Legs.Count),
                new SimulationLogField("emergency", emergency));
        }

        private void ExpireUnpublishedOrders(long tick)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                FreightOrder order = orders[i];
                if (!order.IsOpen || tick - order.LastRefreshTick <= PublicationGraceTicks)
                    continue;

                order.IsOpen = false;
                for (int j = jobs.Count - 1; j >= 0; j--)
                {
                    FreightDeliveryJob job = jobs[j];
                    if (job.Order == order && !job.HasPickedUp && !job.IsTerminal)
                        CancelBeforePickup(job, "demand_publication_expired");
                }
            }
        }

        private void CloseFulfilledOrders()
        {
            for (int i = 0; i < orders.Count; i++)
            {
                FreightOrder order = orders[i];
                if (order.IsOpen && order.Delivered + QuantityEpsilon >= order.Requested)
                {
                    order.IsOpen = false;
                    Log("logistics.demand_satisfied", "Info", order.Requester, null,
                        new SimulationLogField("demandId", order.Id),
                        new SimulationLogField("resource", order.Resource.name),
                        new SimulationLogField("delivered", order.Delivered));
                }
            }
        }

        private List<FreightOrder> GetDispatchOrderPriority()
        {
            List<FreightOrder> result = new List<FreightOrder>();
            for (int i = 0; i < orders.Count; i++)
                if (orders[i] != null && orders[i].IsOpen)
                    result.Add(orders[i]);
            result.Sort((left, right) =>
            {
                int a = IsEmergency(left) ? 0 : 1;
                int b = IsEmergency(right) ? 0 : 1;
                int compare = a.CompareTo(b);
                if (compare != 0) return compare;
                compare = left.OpenedTick.CompareTo(right.OpenedTick);
                return compare != 0 ? compare : string.CompareOrdinal(left.Id, right.Id);
            });
            return result;
        }

        private bool IsEmergency(FreightOrder order)
        {
            return order != null && order.Requester != null && order.Requester.Inventory != null &&
                   order.Requester.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry policy) &&
                   order.Requester.Inventory.GetOnHand(order.Resource) <= policy.emergencyThreshold;
        }

        private float GetDispatchableQuantity(FreightOrder order)
        {
            float actionable = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job.Order == order && !job.IsTerminal && job.State != FreightJobState.Blocked)
                    actionable += job.Quantity;
            }
            return Mathf.Max(0f, order.Requested - order.Delivered - actionable);
        }

        private float GetActionableIncoming(LogisticsStockComponent destination, ResourceDefinition resource)
        {
            float incoming = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job.Destination == destination && job.Resource == resource &&
                    !job.IsTerminal && job.State != FreightJobState.Blocked)
                    incoming += job.Quantity;
            }
            return incoming;
        }

        private float GetCommittedCapacity(
            InventoryComponent destination,
            ResourceDefinition resource,
            FreightDeliveryJob except)
        {
            float total = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job == except || job.IsTerminal || job.Destination == null ||
                    job.Destination.Inventory != destination || job.Resource != resource)
                    continue;
                total += job.Quantity;
            }
            return total;
        }

        private float GetMinimumPickup(FreightOrder order)
        {
            return order != null && order.Requester != null &&
                   order.Requester.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry policy)
                ? policy.minimumPickup
                : float.PositiveInfinity;
        }

        private FreightOrder FindOpenOrder(LogisticsStockComponent requester, ResourceDefinition resource)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                FreightOrder order = orders[i];
                if (order != null && order.IsOpen && order.Requester == requester && order.Resource == resource)
                    return order;
            }
            return null;
        }

        private void FinishCarrier(FreightDeliveryJob job)
        {
            if (job.Carrier != null)
            {
                job.Carrier.RouteRunner?.StopRoute();
                job.Carrier.Complete(job);
                if (job.IsEmergencyExcursion && job.Carrier.Brain != null)
                    job.Carrier.Brain.CompleteWorkExcursion();
            }
        }

        private bool IsKnown(FreightDeliveryJob job) => job != null && jobs.Contains(job);

        private long CurrentTick => SimulationManager.Instance != null
            ? SimulationManager.Instance.CurrentTick
            : 0L;

        private static void Log(string key, string severity,
            UnityEngine.Object subject, UnityEngine.Object target,
            params SimulationLogField[] fields)
        {
            SimulationLogManager.RecordEvent(key, "Logistics", severity, subject, target, fields);
        }

        private sealed class Candidate
        {
            public Candidate(WalkingFreightCarrierComponent carrier,
                LogisticsStockComponent source, float quantity,
                PersonnelRouteEstimate pickupEstimate,
                PersonnelRouteEstimate deliveryEstimate, string stableKey)
            {
                Carrier = carrier;
                Source = source;
                Quantity = quantity;
                PickupEstimate = pickupEstimate;
                DeliveryEstimate = deliveryEstimate;
                Distance = pickupEstimate.Distance + deliveryEstimate.Distance;
                StableKey = stableKey;
            }

            public WalkingFreightCarrierComponent Carrier { get; }
            public LogisticsStockComponent Source { get; }
            public float Quantity { get; }
            public PersonnelRouteEstimate PickupEstimate { get; }
            public PersonnelRouteEstimate DeliveryEstimate { get; }
            public float Distance { get; }
            public string StableKey { get; }

            public static int Compare(Candidate left, Candidate right)
            {
                return FreightCandidateRanking.Compare(
                    left.Quantity, left.Distance, left.StableKey,
                    right.Quantity, right.Distance, right.StableKey);
            }
        }
    }
}
