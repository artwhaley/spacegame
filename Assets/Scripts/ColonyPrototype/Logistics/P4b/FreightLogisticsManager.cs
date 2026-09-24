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
        private readonly object mutationAuthority;
        private float quantity;
        private InventoryReservationToken reservation;

        internal FreightDeliveryJob(FreightAllocation allocation,
            IFreightLegExecution activeLegExecution,
            InventoryReservationToken reservation,
            object mutationAuthority)
        {
            if (allocation == null || activeLegExecution == null || reservation == null ||
                mutationAuthority == null)
                throw new ArgumentNullException(
                    "A freight job requires an allocation, leg execution, source reservation, and manager authority.");

            Allocation = allocation;
            ActiveLegExecution = activeLegExecution;
            this.reservation = reservation;
            this.mutationAuthority = mutationAuthority;
            quantity = allocation.Quantity;
            CurrentLegIndex = 0;
            if (CurrentLegIndex < 0 || CurrentLegIndex >= allocation.RoutePlan.Legs.Count)
                throw new ArgumentException("The freight route must contain a first leg.", nameof(allocation));
            State = FreightJobState.Assigned;
        }

        public FreightAllocation Allocation { get; }
        public IFreightLegExecution ActiveLegExecution { get; private set; }
        public LogisticsRoutePlan RoutePlan => Allocation.RoutePlan;
        public int CurrentLegIndex { get; private set; }
        public LogisticsRouteLeg CurrentLeg => CurrentLegIndex >= 0 &&
            CurrentLegIndex < RoutePlan.Legs.Count ? RoutePlan.Legs[CurrentLegIndex] : null;
        public bool IsAwaitingLegAssignment =>
            !IsTerminal && State == FreightJobState.Assigned && ActiveLegExecution == null;
        public InventoryComponent CargoInventory => ActiveLegExecution != null
            ? ActiveLegExecution.CargoInventory : null;
        public string Id => Allocation.Id;
        public FreightOrder Order => Allocation.Order;
        public ResourceDefinition Resource => Allocation.Resource;
        public LogisticsStockComponent Source => Allocation.Source;
        public LogisticsStockComponent Destination => Allocation.FinalDestination;
        public InventoryReservationToken Reservation => reservation;
        public float Quantity => quantity;
        public bool IsEmergencyWork => ActiveLegExecution != null && ActiveLegExecution.IsEmergency;
        public FreightJobState State { get; private set; }
        public bool HasPickedUp { get; private set; }
        public bool IsTerminal => State == FreightJobState.Completed || State == FreightJobState.Cancelled;
        public long RetryAtTick { get; private set; }

        internal void SetState(object authority, FreightJobState state)
        {
            VerifyAuthority(authority);
            State = state;
        }

        internal void SetHasPickedUp(object authority, bool hasPickedUp)
        {
            VerifyAuthority(authority);
            HasPickedUp = hasPickedUp;
        }

        internal void SetQuantity(object authority, float newQuantity)
        {
            VerifyAuthority(authority);
            quantity = Mathf.Max(0f, newQuantity);
        }

        internal void SetReservation(object authority, InventoryReservationToken newReservation)
        {
            VerifyAuthority(authority);
            reservation = newReservation;
        }

        internal void SetActiveLegExecution(object authority, IFreightLegExecution execution)
        {
            VerifyAuthority(authority);
            ActiveLegExecution = execution;
        }

        internal void SetRetryAtTick(object authority, long retryAtTick)
        {
            VerifyAuthority(authority);
            RetryAtTick = retryAtTick;
        }

        internal bool AdvanceLeg(object authority)
        {
            VerifyAuthority(authority);
            if (CurrentLegIndex + 1 >= RoutePlan.Legs.Count)
                return false;
            CurrentLegIndex++;
            return true;
        }

        private void VerifyAuthority(object authority)
        {
            if (!ReferenceEquals(authority, mutationAuthority))
                throw new InvalidOperationException("Only the owning FreightLogisticsManager may mutate this job.");
        }
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
        private readonly object jobMutationAuthority = new object();

        [NonSerialized] private readonly List<FreightOrder> orders = new List<FreightOrder>();
        [NonSerialized] private readonly List<FreightDeliveryJob> jobs = new List<FreightDeliveryJob>();

        public static FreightLogisticsManager Instance { get; private set; }
        public IReadOnlyList<FreightOrder> Orders => orders;
        public IReadOnlyList<FreightDeliveryJob> Jobs => jobs;
        public int SimulationTickPriority => 310;

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

        internal bool ReportExecution(
            FreightDeliveryJob job,
            IFreightLegExecution execution,
            FreightExecutionReport report,
            FreightFailureReason failureReason = FreightFailureReason.None,
            string detail = null)
        {
            if (!IsCurrentExecution(job, execution))
                return false;

            switch (report)
            {
                case FreightExecutionReport.PickupRouteStarted:
                    if (job.State != FreightJobState.Assigned || job.HasPickedUp)
                        return false;
                    TransitionJob(job, FreightJobState.TravelingToPickup);
                    return true;

                case FreightExecutionReport.ProviderAtSource:
                    if (job.State != FreightJobState.TravelingToPickup || job.HasPickedUp)
                        return false;
                    return PickupAtCurrentLeg(job);

                case FreightExecutionReport.LoadedRouteStarted:
                    if (!job.HasPickedUp ||
                        (job.State != FreightJobState.PickingUp && job.State != FreightJobState.Blocked))
                    {
                        return false;
                    }
                    job.SetRetryAtTick(jobMutationAuthority, 0L);
                    TransitionJob(job, FreightJobState.TravelingToDropoff);
                    return true;

                case FreightExecutionReport.LoadedLegArrived:
                    if (!job.HasPickedUp || job.State != FreightJobState.TravelingToDropoff)
                        return false;
                    return CompleteCurrentLeg(job);

                case FreightExecutionReport.Failed:
                    if (failureReason == FreightFailureReason.None)
                        return false;
                    if (job.HasPickedUp)
                        BlockJob(job, failureReason, detail);
                    else
                        CancelBeforePickup(job, failureReason, detail);
                    return true;

                default:
                    return false;
            }
        }

        internal WorkReleaseDisposition RequestExecutionRelease(
            FreightDeliveryJob job,
            IFreightLegExecution execution,
            WorkReleaseReason reason)
        {
            if (!IsCurrentExecution(job, execution))
                return WorkReleaseDisposition.ReleasedNow;
            if (job.HasPickedUp)
                return WorkReleaseDisposition.Deferred;

            CancelBeforePickup(job, FreightFailureReason.WorkReleasedBeforePickup, reason.ToString());
            return WorkReleaseDisposition.ReleasedNow;
        }

        private bool IsCurrentExecution(
            FreightDeliveryJob job,
            IFreightLegExecution execution)
        {
            return IsKnown(job) && execution != null && execution.IsActive &&
                   ReferenceEquals(job.ActiveLegExecution, execution) &&
                   job.CurrentLegIndex == execution.LegIndex && !job.IsTerminal;
        }

        private void TransitionJob(
            FreightDeliveryJob job,
            FreightJobState state,
            FreightFailureReason reason = FreightFailureReason.None,
            string detail = null)
        {
            if (!IsKnown(job) || job.State == state)
                return;

            job.SetState(jobMutationAuthority, state);
            IFreightLegExecution execution = job.ActiveLegExecution;
            string eventKey = state == FreightJobState.Blocked ? "logistics.job_blocked" :
                state == FreightJobState.Cancelled ? "logistics.job_cancelled" :
                "logistics.job_state_changed";
            Log(eventKey,
                state == FreightJobState.Blocked || state == FreightJobState.Cancelled ? "Warning" : "Info",
                execution != null ? execution.ProviderContext : null, job.CurrentLeg?.Destination,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("demandId", job.Order != null ? job.Order.Id : string.Empty),
                new SimulationLogField("state", state),
                new SimulationLogField("reason", FailureCode(reason)),
                new SimulationLogField("detail", detail ?? string.Empty),
                new SimulationLogField("quantity", job.Quantity),
                new SimulationLogField("routeDistance", job.RoutePlan.TotalEstimatedLoadedDistance),
                new SimulationLogField("positioningDistance", execution != null ? execution.PositioningDistance : 0f),
                new SimulationLogField("currentLegIndex", job.CurrentLegIndex),
                new SimulationLogField("freightLeg", state == FreightJobState.TravelingToPickup ||
                    state == FreightJobState.PickingUp ? "pickup" :
                    state == FreightJobState.TravelingToDropoff || state == FreightJobState.DroppingOff
                        ? "delivery" : "none"),
                new SimulationLogField("reservationActive", job.Reservation != null && job.Reservation.IsActive));
        }

        private static string FailureCode(FreightFailureReason reason)
        {
            switch (reason)
            {
                case FreightFailureReason.PickupPreconditionFailed: return "pickup_precondition_failed";
                case FreightFailureReason.AllocationNoLongerFits: return "allocation_no_longer_fits";
                case FreightFailureReason.WorkerInventoryMissing: return "worker_inventory_missing";
                case FreightFailureReason.ReservedStockUnavailable: return "reserved_stock_unavailable";
                case FreightFailureReason.DestinationOrCarrierUnavailable: return "destination_or_carrier_unavailable";
                case FreightFailureReason.OrderFulfilledCarrierRetainsExcessCargo:
                    return "order_fulfilled_carrier_retains_excess_cargo";
                case FreightFailureReason.DestinationHasNoCapacity: return "destination_has_no_capacity";
                case FreightFailureReason.CarrierRetainsUnacceptedCargo: return "carrier_retains_unaccepted_cargo";
                case FreightFailureReason.PickupAnchorMissing: return "pickup_anchor_missing";
                case FreightFailureReason.DropoffAnchorMissing: return "dropoff_anchor_missing";
                case FreightFailureReason.PersonnelRouteRunnerMissing: return "personnel_route_runner_missing";
                case FreightFailureReason.PickupRouteUnavailable: return "pickup_route_unavailable";
                case FreightFailureReason.DropoffRouteUnavailable: return "dropoff_route_unavailable";
                case FreightFailureReason.FreightRouteFailed: return "freight_route_failed";
                case FreightFailureReason.WorkReleasedBeforePickup: return "work_release_before_pickup";
                case FreightFailureReason.StageReservationFailed: return "stage_reservation_failed";
                case FreightFailureReason.DemandPublicationExpired: return "demand_publication_expired";
                default: return string.Empty;
            }
        }

        private bool PickupAtCurrentLeg(FreightDeliveryJob job)
        {
            LogisticsRouteLeg leg = job.CurrentLeg;
            InventoryComponent sourceInventory = leg != null && leg.Origin != null
                ? leg.Origin.Inventory : null;
            InventoryComponent cargoInventory = job.CargoInventory;
            if (leg == null || sourceInventory == null || leg.Destination == null ||
                leg.Destination.Inventory == null || job.Reservation == null ||
                !job.Reservation.IsActive || job.Reservation.Owner != sourceInventory)
            {
                CancelBeforePickup(job, FreightFailureReason.PickupPreconditionFailed);
                return false;
            }

            TransitionJob(job, FreightJobState.PickingUp);
            if (cargoInventory == null)
            {
                CancelBeforePickup(job, FreightFailureReason.WorkerInventoryMissing);
                return false;
            }

            if (leg.Destination == job.Destination)
            {
                float otherInbound = GetCommittedCapacity(job.Destination.Inventory,
                    job.Resource, job);
                float availableDestinationCapacity = Mathf.Max(0f,
                    job.Destination.Inventory.GetFreeCapacity(job.Resource) - otherInbound);
                if (availableDestinationCapacity + QuantityEpsilon < job.Quantity)
                {
                    CancelBeforePickup(job, FreightFailureReason.AllocationNoLongerFits);
                    return false;
                }
            }

            if (job.Reservation.Remaining + QuantityEpsilon < job.Quantity)
            {
                CancelBeforePickup(job, FreightFailureReason.AllocationNoLongerFits);
                return false;
            }

            float moved = sourceInventory.TransferOwnedToAndReserveDestination(
                job.Reservation, cargoInventory, job.Quantity, out InventoryReservationToken cargoReservation);
            if (moved <= QuantityEpsilon || cargoReservation == null)
            {
                CancelBeforePickup(job, FreightFailureReason.ReservedStockUnavailable);
                return false;
            }

            if (job.Reservation.IsActive)
                sourceInventory.ReleaseOwned(job.Reservation);
            float reduced = Mathf.Max(0f, job.Quantity - moved);
            job.Order.Committed = Mathf.Max(0f, job.Order.Committed - reduced);
            job.SetQuantity(jobMutationAuthority, moved);
            job.SetReservation(jobMutationAuthority, cargoReservation);
            job.SetHasPickedUp(jobMutationAuthority, true);
            Log("logistics.pickup_completed", "Info", job.ActiveLegExecution.ProviderContext, leg.Origin,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("quantity", moved),
                new SimulationLogField("currentLegIndex", job.CurrentLegIndex),
                new SimulationLogField("demandId", job.Order.Id));
            return true;
        }

        private bool CompleteCurrentLeg(FreightDeliveryJob job)
        {
            LogisticsRouteLeg leg = job.CurrentLeg;
            InventoryComponent destinationInventory = leg != null && leg.Destination != null
                ? leg.Destination.Inventory : null;
            InventoryComponent cargoInventory = job.CargoInventory;
            if (leg == null || destinationInventory == null || cargoInventory == null ||
                job.Reservation == null || !job.Reservation.IsActive ||
                job.Reservation.Owner != cargoInventory)
            {
                BlockJob(job, FreightFailureReason.DestinationOrCarrierUnavailable);
                return false;
            }

            bool finalLeg = leg.Destination == job.Destination;
            float requestedTransfer = job.Quantity;
            float orderRemainderBeforeTransfer = float.PositiveInfinity;
            if (finalLeg)
            {
                orderRemainderBeforeTransfer = Mathf.Max(0f,
                    job.Order.Requested - job.Order.Delivered);
                requestedTransfer = Mathf.Min(job.Quantity, orderRemainderBeforeTransfer);
                if (requestedTransfer <= QuantityEpsilon)
                {
                    BlockJob(job, FreightFailureReason.OrderFulfilledCarrierRetainsExcessCargo);
                    return false;
                }

                float otherInbound = GetCommittedCapacity(destinationInventory, job.Resource, job);
                float availableCapacity = Mathf.Max(0f,
                    destinationInventory.GetFreeCapacity(job.Resource) - otherInbound);
                requestedTransfer = Mathf.Min(requestedTransfer, availableCapacity);
                if (requestedTransfer <= QuantityEpsilon)
                {
                    BlockJob(job, FreightFailureReason.DestinationHasNoCapacity);
                    return false;
                }
            }
            else if (destinationInventory.GetFreeCapacity(job.Resource) + QuantityEpsilon < requestedTransfer)
            {
                BlockJob(job, FreightFailureReason.DestinationHasNoCapacity);
                return false;
            }

            TransitionJob(job, FreightJobState.DroppingOff);
            int completedLegIndex = job.CurrentLegIndex;
            InventoryReservationToken carriedReservation = job.Reservation;
            InventoryReservationToken stagedReservation = null;
            float moved = finalLeg
                ? cargoInventory.TransferOwnedTo(carriedReservation, destinationInventory, requestedTransfer)
                : cargoInventory.TransferOwnedToAndReserveDestination(
                    carriedReservation, destinationInventory, requestedTransfer, out stagedReservation);
            if (moved <= QuantityEpsilon)
            {
                BlockJob(job, FreightFailureReason.DestinationHasNoCapacity);
                return false;
            }

            if (!finalLeg)
            {
                if (moved + QuantityEpsilon < requestedTransfer || stagedReservation == null)
                {
                    BlockJob(job, FreightFailureReason.StageReservationFailed);
                    return false;
                }

                job.SetReservation(jobMutationAuthority, stagedReservation);
                job.SetHasPickedUp(jobMutationAuthority, false);
                if (!job.AdvanceLeg(jobMutationAuthority))
                {
                    BlockJob(job, FreightFailureReason.StageReservationFailed);
                    return false;
                }

                TransitionJob(job, FreightJobState.Assigned);
                Log("logistics.leg_staged", "Info", job.ActiveLegExecution.ProviderContext, leg.Destination,
                    new SimulationLogField("jobId", job.Id),
                    new SimulationLogField("allocationId", job.Id),
                    new SimulationLogField("completedLegIndex", completedLegIndex),
                    new SimulationLogField("nextLegIndex", job.CurrentLegIndex),
                    new SimulationLogField("resource", job.Resource.name),
                    new SimulationLogField("quantity", moved),
                    new SimulationLogField("demandId", job.Order.Id));
                FinishExecution(job);
                job.SetActiveLegExecution(jobMutationAuthority, null);
                return true;
            }

            job.SetReservation(jobMutationAuthority,
                carriedReservation.IsActive ? carriedReservation : null);
            job.SetQuantity(jobMutationAuthority, job.Quantity - moved);
            job.Order.Committed = Mathf.Max(0f, job.Order.Committed - moved);
            job.Order.Delivered += moved;
            Log("logistics.delivery_completed", "Info", job.ActiveLegExecution.ProviderContext, leg.Destination,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("currentLegIndex", completedLegIndex),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("quantity", moved),
                new SimulationLogField("demandId", job.Order.Id));

            if (job.Quantity > QuantityEpsilon)
            {
                FreightFailureReason reason = orderRemainderBeforeTransfer <= moved + QuantityEpsilon
                    ? FreightFailureReason.OrderFulfilledCarrierRetainsExcessCargo
                    : destinationInventory.GetFreeCapacity(job.Resource) <= QuantityEpsilon
                        ? FreightFailureReason.DestinationHasNoCapacity
                        : FreightFailureReason.CarrierRetainsUnacceptedCargo;
                BlockJob(job, reason);
                return false;
            }

            job.SetReservation(jobMutationAuthority, null);
            job.SetHasPickedUp(jobMutationAuthority, false);
            TransitionJob(job, FreightJobState.Completed);
            CloseFulfilledOrders();
            FinishExecution(job);
            job.SetActiveLegExecution(jobMutationAuthority, null);
            return true;
        }

        private void CancelBeforePickup(
            FreightDeliveryJob job,
            FreightFailureReason reason,
            string detail = null)
        {
            if (!IsKnown(job) || job.HasPickedUp || job.IsTerminal)
                return;

            job.CurrentLeg?.Origin?.Inventory?.ReleaseOwned(job.Reservation);
            if (job.Order != null)
                job.Order.Committed = Mathf.Max(0f, job.Order.Committed - job.Quantity);
            job.SetReservation(jobMutationAuthority, null);
            TransitionJob(job, FreightJobState.Cancelled, reason, detail);
            FinishExecution(job);
            job.SetActiveLegExecution(jobMutationAuthority, null);
        }

        private void BlockJob(
            FreightDeliveryJob job,
            FreightFailureReason reason,
            string detail = null)
        {
            if (!IsKnown(job) || job.IsTerminal)
                return;
            job.SetRetryAtTick(jobMutationAuthority,
                reason == FreightFailureReason.OrderFulfilledCarrierRetainsExcessCargo
                    ? long.MaxValue
                    : CurrentTick + BlockedRetryTicks);
            TransitionJob(job, FreightJobState.Blocked, reason, detail);
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
            IReadOnlyList<WalkingFreightWorkService> services = WalkingFreightWorkService.Active;
            IReadOnlyList<LogisticsStockComponent> supplies = LogisticsStockComponent.Active;
            for (int serviceIndex = 0; serviceIndex < services.Count; serviceIndex++)
            {
                WalkingFreightWorkService service = services[serviceIndex];
                if (service == null || !service.isActiveAndEnabled)
                    continue;

                for (int s = 0; s < supplies.Count; s++)
                {
                    LogisticsStockComponent source = supplies[s];
                    if (source == null || source == order.Requester || source.Inventory == null ||
                        !source.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry supplyPolicy) ||
                        supplyPolicy.role == LogisticsStockRole.Consumer)
                        continue;

                    float destinationSpace = order.Requester.Inventory.GetFreeCapacity(order.Resource) -
                        GetCommittedCapacity(order.Requester.Inventory, order.Resource, null);
                    float desiredQuantity = Mathf.Min(dispatchable,
                        Mathf.Min(source.Inventory.GetAvailable(order.Resource), destinationSpace));
                    if (desiredQuantity + QuantityEpsilon < GetMinimumPickup(order) ||
                        !service.TryQuote(source, order.Requester, order.Resource,
                            desiredQuantity, emergencyOnly, out FreightWorkQuote quote))
                        continue;

                    float quantity = Mathf.Min(desiredQuantity, quote.MaximumUsefulQuantity);
                    if (quantity + QuantityEpsilon < GetMinimumPickup(order))
                        continue;

                    Candidate candidate = new Candidate(service, quote, source, quantity,
                        service.GetStableKey() + "/" + source.GetStableKey() + "/" + order.Id);
                    if (best == null || Candidate.Compare(candidate, best) < 0)
                        best = candidate;
                }
            }
            return best;
        }

        private void AcceptCandidate(FreightOrder order, Candidate candidate, bool emergency)
        {
            if (!candidate.Source.Inventory.TryReserveOwned(
                    order.Resource, candidate.Quantity, out InventoryReservationToken reservation))
            {
                return;
            }

            if (!candidate.Service.TryAcceptQuote(candidate.Quote, candidate.Quantity,
                    out WalkingFreightExecution walkingExecution))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                return;
            }

            string jobId = "freight-job-" + (++nextJobId).ToString("D6");
            Transform sourceAnchor = candidate.Source.FreightAnchor;
            Transform destinationAnchor = order.Requester.FreightAnchor;
            LogisticsRoutePlan routePlan = new LogisticsRoutePlan(order,
                new[]
                {
                    new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                        candidate.Source, order.Requester,
                        candidate.Quote.LoadedCargoTravelCost)
                });
            FreightAllocation allocation = new FreightAllocation(
                jobId, order, candidate.Source, candidate.Quantity, routePlan);
            FreightDeliveryJob job = new FreightDeliveryJob(
                allocation, walkingExecution, reservation, jobMutationAuthority);
            if (!walkingExecution.PrepareCargo(order.Resource))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                walkingExecution.CancelBeforeAssignment();
                return;
            }
            order.Committed += candidate.Quantity;
            jobs.Add(job);
            if (!walkingExecution.Assign(job))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                order.Committed = Mathf.Max(0f, order.Committed - candidate.Quantity);
                job.SetState(jobMutationAuthority, FreightJobState.Cancelled);
                walkingExecution.CancelBeforeAssignment();
                return;
            }

            Log("logistics.job_assigned", "Info", candidate.Service, order.Requester,
                new SimulationLogField("jobId", job.Id),
                new SimulationLogField("allocationId", job.Id),
                new SimulationLogField("demandId", order.Id),
                new SimulationLogField("resource", order.Resource.name),
                new SimulationLogField("quantity", candidate.Quantity),
                new SimulationLogField("source", candidate.Source.name),
                new SimulationLogField("distance", candidate.Distance),
                new SimulationLogField("positioningDistance", candidate.Quote.PositioningCost),
                new SimulationLogField("loadedCargoDistance", candidate.Quote.LoadedCargoTravelCost),
                new SimulationLogField("pickupAnchor", sourceAnchor.name),
                new SimulationLogField("pickupDistance", candidate.Quote.PositioningCost),
                new SimulationLogField("dropoffAnchor", destinationAnchor.name),
                new SimulationLogField("dropoffDistance", candidate.Quote.LoadedCargoTravelCost),
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
                        CancelBeforePickup(job, FreightFailureReason.DemandPublicationExpired);
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

        private void FinishExecution(FreightDeliveryJob job)
        {
            if (job != null && job.ActiveLegExecution != null)
                job.ActiveLegExecution.Complete(job);
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
            public Candidate(WalkingFreightWorkService service,
                FreightWorkQuote quote, LogisticsStockComponent source,
                float quantity, string stableKey)
            {
                Service = service;
                Quote = quote;
                Source = source;
                Quantity = quantity;
                Distance = quote.TotalServiceCost;
                StableKey = stableKey;
            }

            public WalkingFreightWorkService Service { get; }
            public FreightWorkQuote Quote { get; }
            public LogisticsStockComponent Source { get; }
            public float Quantity { get; }
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
