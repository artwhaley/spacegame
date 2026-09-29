using System;
using System.Collections.Generic;
using System.Globalization;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Small parent-demand ledger. Child identity and custody remain manager-owned.</summary>
    public sealed class FreightDemandAccounting
    {
        private const float Epsilon = 0.0001f;

        public FreightDemandAccounting(float requested)
        {
            if (float.IsNaN(requested) || float.IsInfinity(requested) || requested <= 0f)
                throw new ArgumentOutOfRangeException(nameof(requested));
            Requested = requested;
        }

        public float Requested { get; private set; }
        public float Delivered { get; private set; }
        public float Committed { get; private set; }
        public float Retired { get; private set; }
        public float Uncovered => Mathf.Max(0f, Requested - Delivered - Committed - Retired);

        public bool SetUncovered(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                return false;
            float current = Uncovered;
            if (amount < current)
            {
                Retired += current - amount;
            }
            else if (amount > current)
            {
                float increase = amount - current;
                float restore = Mathf.Min(increase, Retired);
                Retired -= restore;
                Requested += increase - restore;
            }
            return IsBalanced;
        }

        public float RetireUncovered()
        {
            float retired = Uncovered;
            Retired += retired;
            return retired;
        }

        public bool TryCommit(float amount)
        {
            if (!IsFinitePositive(amount) || amount > Uncovered + Epsilon)
                return false;
            Committed += Mathf.Min(amount, Uncovered);
            return IsBalanced;
        }

        public bool RecordDelivery(float amount)
        {
            if (!IsFinitePositive(amount) || amount > Committed + Epsilon)
                return false;
            float accepted = Mathf.Min(amount, Committed);
            Committed -= accepted;
            Delivered += accepted;
            return IsBalanced;
        }

        public bool ReleaseCommitment(float amount)
        {
            if (!IsFinitePositive(amount) || amount > Committed + Epsilon)
                return false;
            Committed -= Mathf.Min(amount, Committed);
            return IsBalanced;
        }

        public bool IsBalanced => Delivered >= -Epsilon && Committed >= -Epsilon &&
            Retired >= -Epsilon && Uncovered >= -Epsilon &&
            Mathf.Abs(Requested - Delivered - Committed - Retired - Uncovered) <= Epsilon;

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

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

    public enum FreightDemandState
    {
        Dispatching,
        WaitingForAcceptedChildren,
        Satisfied,
        TargetSatisfied,
        Retiring,
        Cancelled,
        CancelledWithDeliveries
    }

    public sealed class FreightOrder
    {
        private readonly FreightDemandAccounting accounting;

        internal FreightOrder(string id, LogisticsStockComponent requester,
            ResourceDefinition resource, float requested, long openedTick)
        {
            Id = id;
            Requester = requester;
            Resource = resource;
            accounting = new FreightDemandAccounting(requested);
            OpenedTick = openedTick;
            LastRefreshTick = openedTick;
            IsOpen = true;
        }

        public string Id { get; }
        public LogisticsStockComponent Requester { get; }
        public ResourceDefinition Resource { get; }
        public float Requested => accounting.Requested;
        public float Delivered => accounting.Delivered;
        public float Committed => accounting.Committed;
        public long OpenedTick { get; }
        public long LastRefreshTick { get; internal set; }
        public bool IsOpen { get; internal set; }
        public bool IsRetiring { get; internal set; }
        public bool IsPolicyRetired { get; internal set; }
        public float Uncovered => accounting.Uncovered;
        public float Retired => accounting.Retired;
        public FreightDemandState State => IsRetiring
            ? Committed > 0.0001f ? FreightDemandState.Retiring
                : Delivered > 0.0001f ? FreightDemandState.CancelledWithDeliveries
                : FreightDemandState.Cancelled
            : IsPolicyRetired && Committed > 0.0001f
                ? FreightDemandState.WaitingForAcceptedChildren
                : IsPolicyRetired ? FreightDemandState.TargetSatisfied
            : IsOpen && Uncovered <= 0.0001f && Committed > 0.0001f
                ? FreightDemandState.WaitingForAcceptedChildren
                : IsOpen ? FreightDemandState.Dispatching
                : Delivered + 0.0001f >= Requested ? FreightDemandState.Satisfied
                : Delivered > 0.0001f ? FreightDemandState.CancelledWithDeliveries
                : FreightDemandState.Cancelled;
        internal bool TryCommit(float amount) => accounting.TryCommit(amount);
        internal bool RecordDelivery(float amount) => accounting.RecordDelivery(amount);
        internal bool ReleaseCommitment(float amount) => accounting.ReleaseCommitment(amount);
        internal bool SetUncovered(float amount) => accounting.SetUncovered(amount);
        internal float RetireUncovered() => accounting.RetireUncovered();
    }

    /// <summary>Read-only physical claims and fulfillment facts for one child shipment.</summary>
    public readonly struct FreightCustodySnapshot
    {
        internal FreightCustodySnapshot(
            InventoryComponent originInventory,
            float originReservedQuantity,
            InventoryComponent carrierInventory,
            float carriedQuantity,
            InventoryComponent stagingInventory,
            float stagedQuantity,
            float deliveredQuantity,
            float outstandingQuantity)
        {
            OriginInventory = originInventory;
            OriginReservedQuantity = originReservedQuantity;
            CarrierInventory = carrierInventory;
            CarriedQuantity = carriedQuantity;
            StagingInventory = stagingInventory;
            StagedQuantity = stagedQuantity;
            DeliveredQuantity = deliveredQuantity;
            OutstandingQuantity = outstandingQuantity;
        }

        public InventoryComponent OriginInventory { get; }
        public float OriginReservedQuantity { get; }
        public InventoryComponent CarrierInventory { get; }
        public float CarriedQuantity { get; }
        public InventoryComponent StagingInventory { get; }
        public float StagedQuantity { get; }
        public float DeliveredQuantity { get; }
        public float OutstandingQuantity { get; }
    }

    public sealed class FreightDeliveryJob
    {
        private readonly object mutationAuthority;
        private float quantity;
        private float deliveredQuantity;
        private InventoryReservationToken reservation;
        private string activePersonnelRouteId;
        private string activePersonnelRouteStableId;
        private string lastFailureReason = string.Empty;
        private string lastFailureDetail = string.Empty;

        internal FreightDeliveryJob(FreightAllocation allocation,
            IFreightLegExecution activeLegExecution,
            InventoryReservationToken reservation,
            object mutationAuthority)
        {
            if (allocation == null || reservation == null || mutationAuthority == null)
                throw new ArgumentNullException(
                    "A freight job requires an allocation, source reservation, and manager authority.");

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
        public float OriginalQuantity => Allocation.Quantity;
        public float DeliveredQuantity => deliveredQuantity;
        public IFreightLegExecution ActiveLegExecution { get; private set; }
        public LogisticsRoutePlan RoutePlan => Allocation.RoutePlan;
        public int CurrentLegIndex { get; private set; }
        public LogisticsRouteLeg CurrentLeg => CurrentLegIndex >= 0 &&
            CurrentLegIndex < RoutePlan.Legs.Count ? RoutePlan.Legs[CurrentLegIndex] : null;
        public bool IsAwaitingLegAssignment =>
            !IsTerminal && State == FreightJobState.Assigned && ActiveLegExecution == null;
        public InventoryComponent CargoInventory => ActiveLegExecution != null
            ? ActiveLegExecution.CargoInventory : null;
        public InventoryComponent CustodyInventory => Reservation != null && Reservation.IsActive
            ? Reservation.Owner : null;
        public bool HasLiveCustodyClaim
        {
            get
            {
                FreightCustodySnapshot custody = Custody;
                return custody.OriginReservedQuantity + custody.CarriedQuantity +
                       custody.StagedQuantity > 0.0001f;
            }
        }
        public bool HasLostCustodyClaim =>
            (Reservation != null && Reservation.IsActive && Reservation.Owner == null) ||
            (State == FreightJobState.Blocked && lastFailureReason == "accepted_cargo_missing");
        public FreightCustodySnapshot Custody => CreateCustodySnapshot();
        public float CarriedQuantity => Custody.CarriedQuantity;
        public float StagedQuantity => Custody.StagedQuantity;
        public float AcceptedWorkerRemainder
        {
            get
            {
                IFreightLegExecution execution = ActiveLegExecution;
                if (execution == null || !execution.CompletesAcceptedQuantityAcrossLoads)
                    return 0f;
                if (State == FreightJobState.Blocked && RetryAtTick == long.MaxValue)
                    return quantity;
                FreightLegProgress progress = LegProgress;
                return progress != null && progress.LegIndex == CurrentLegIndex
                    ? progress.RemainingAtOrigin + progress.InCarrierQuantity
                    : quantity;
            }
        }
        public bool HasPendingWorkerRelease => ActiveLegExecution != null &&
            ActiveLegExecution.HasPendingWorkerRelease;
        public bool IsWaitingForProvider => ActiveLegExecution != null &&
            !ActiveLegExecution.ProviderAvailable && ActiveLegExecution.ExecutorAvailable;
        public string ProviderWaitReason => IsWaitingForProvider ? "provider_unavailable" : string.Empty;
        public UnityEngine.Object CurrentLegProvider => ActiveLegExecution != null
            ? ActiveLegExecution.ProviderContext : null;
        public string LastFailureReason => lastFailureReason ?? string.Empty;
        public string LastFailureDetail => lastFailureDetail ?? string.Empty;
        public string RecoveryAction => State != FreightJobState.Blocked ? "none" :
            RetryAtTick == long.MaxValue ? "manual_recovery_required" : "retry_after_tick";
        public FreightOrder Order => Allocation.Order;
        public ResourceDefinition Resource => Allocation.Resource;
        public LogisticsStockComponent Source => Allocation.Source;
        public LogisticsStockComponent Destination => Allocation.FinalDestination;
        public InventoryReservationToken Reservation => reservation;
        internal FreightLegProgress LegProgress { get; private set; }
        public float Quantity => quantity;
        public bool IsEmergencyWork => ActiveLegExecution != null && ActiveLegExecution.IsEmergency;
        public FreightJobState State { get; private set; }
        public bool HasPickedUp { get; private set; }
        public bool IsTerminal => State == FreightJobState.Completed || State == FreightJobState.Cancelled;
        public long RetryAtTick { get; private set; }
        public string ActivePersonnelRouteId => activePersonnelRouteId ?? string.Empty;
        public string ActivePersonnelRouteStableId => activePersonnelRouteStableId ?? string.Empty;

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

        internal void AddDeliveredQuantity(object authority, float amount)
        {
            VerifyAuthority(authority);
            deliveredQuantity += Mathf.Max(0f, amount);
        }

        internal void SetReservation(object authority, InventoryReservationToken newReservation)
        {
            VerifyAuthority(authority);
            reservation = newReservation;
        }

        internal void SetLegProgress(object authority, FreightLegProgress progress)
        {
            VerifyAuthority(authority);
            LegProgress = progress;
        }

        internal void SetActiveLegExecution(object authority, IFreightLegExecution execution)
        {
            VerifyAuthority(authority);
            ActiveLegExecution = execution;
        }

        internal void SetActivePersonnelRoute(
            object authority,
            string routeId,
            string routeStableId)
        {
            VerifyAuthority(authority);
            activePersonnelRouteId = routeId;
            activePersonnelRouteStableId = routeStableId;
        }

        internal void SetRetryAtTick(object authority, long retryAtTick)
        {
            VerifyAuthority(authority);
            RetryAtTick = retryAtTick;
        }

        internal void SetLastFailure(object authority, string reason, string detail)
        {
            VerifyAuthority(authority);
            lastFailureReason = reason ?? string.Empty;
            lastFailureDetail = detail ?? string.Empty;
        }

        private FreightCustodySnapshot CreateCustodySnapshot()
        {
            InventoryComponent originInventory = null;
            InventoryComponent carrierInventory = null;
            InventoryComponent stagingInventory = null;
            float originQuantity = 0f;
            float carriedQuantity = 0f;
            float stagedQuantity = 0f;

            FreightLegProgress progress = LegProgress;
            if (progress != null && progress.LegIndex == CurrentLegIndex)
            {
                AddClaim(progress.OriginReservation, ref originInventory, ref originQuantity);
                AddClaim(progress.CurrentCarrierReservation, ref carrierInventory, ref carriedQuantity);
                AddClaim(progress.DestinationReservation, ref stagingInventory, ref stagedQuantity);
                for (int index = 0; index < progress.AdditionalDestinationReservations.Count; index++)
                    AddClaim(progress.AdditionalDestinationReservations[index],
                        ref stagingInventory, ref stagedQuantity);
            }
            else if (reservation != null && reservation.IsActive)
            {
                InventoryComponent owner = reservation.Owner;
                if (HasPickedUp && ActiveLegExecution != null &&
                    owner == ActiveLegExecution.CargoInventory)
                    AddClaim(reservation, ref carrierInventory, ref carriedQuantity);
                else if (CurrentLegIndex > 0 && CurrentLeg != null &&
                         owner == CurrentLeg.Origin.Inventory)
                    AddClaim(reservation, ref stagingInventory, ref stagedQuantity);
                else if (CurrentLeg != null && owner == CurrentLeg.Origin.Inventory)
                    AddClaim(reservation, ref originInventory, ref originQuantity);
            }

            return new FreightCustodySnapshot(originInventory, originQuantity,
                carrierInventory, carriedQuantity, stagingInventory, stagedQuantity,
                deliveredQuantity, quantity);
        }

        private static void AddClaim(InventoryReservationToken token,
            ref InventoryComponent inventory, ref float quantity)
        {
            if (token == null || !token.IsActive || token.Owner == null)
                return;
            inventory = token.Owner;
            quantity += token.Remaining;
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

    /// <summary>Manager-owned physical custody accounting for one walking leg.</summary>
    internal sealed class FreightLegProgress
    {
        public FreightLegProgress(int legIndex, float totalQuantity,
            InventoryReservationToken originReservation)
        {
            LegIndex = legIndex;
            TotalQuantity = totalQuantity;
            RemainingAtOrigin = totalQuantity;
            OriginReservation = originReservation;
        }

        public int LegIndex { get; }
        public float TotalQuantity { get; }
        public float RemainingAtOrigin { get; set; }
        public float InCarrierQuantity { get; set; }
        public float ArrivedAtDestination { get; set; }
        public InventoryReservationToken OriginReservation { get; set; }
        public InventoryReservationToken CurrentCarrierReservation { get; set; }
        public InventoryReservationToken DestinationReservation { get; set; }
        public readonly List<InventoryReservationToken> AdditionalDestinationReservations =
            new List<InventoryReservationToken>();
        public int TripsStarted { get; set; }
    }

    /// <summary>Owns local stock orders and walking freight allocations for P4b.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Freight Logistics Manager")]
    public sealed class FreightLogisticsManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float QuantityEpsilon = 0.0001f;
        private const long PublicationGraceTicks = 3;
        private const long BlockedRetryTicks = 5;
        private const int TerminalHistoryLimit = 128;
        private static long nextOrderId;
        private static long nextAllocationId;
        private static long nextExecutionId;
        private readonly object jobMutationAuthority = new object();

        [NonSerialized] private readonly List<FreightOrder> orders = new List<FreightOrder>();
        [NonSerialized] private readonly List<FreightDeliveryJob> jobs = new List<FreightDeliveryJob>();
        [NonSerialized] private readonly HashSet<string> walkingLegFailuresLogged =
            new HashSet<string>(StringComparer.Ordinal);

        public static FreightLogisticsManager Instance { get; private set; }
        public IReadOnlyList<FreightOrder> Orders => orders;
        public IReadOnlyList<FreightDeliveryJob> Jobs => jobs;
        public IReadOnlyList<FreightOrder> ActiveOrders => SelectOrders(true);
        public IReadOnlyList<FreightOrder> RecentOrders => SelectOrders(false);
        public IReadOnlyList<FreightDeliveryJob> ActiveJobs => SelectJobs(true);
        public IReadOnlyList<FreightDeliveryJob> RecentJobs => SelectJobs(false);
        public int SimulationTickPriority => SimulationTickPriorities.LogisticsDispatch;

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
            nextAllocationId = 0L;
            nextExecutionId = 0L;
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
            IReadOnlyList<LogisticsStockPolicyEntry> policies = stock.Policies;
            for (int i = 0; i < policies.Count; i++)
            {
                LogisticsStockPolicyEntry policy = policies[i];
                if (policy == null || policy.resource == null ||
                    policy.role != LogisticsStockRole.Consumer)
                    continue;

                FreightOrder current = FindOpenOrder(stock, policy.resource);
                if (current != null)
                {
                    current.LastRefreshTick = tick;
                    if (current.IsRetiring)
                        continue;
                    bool wasPolicyRetired = current.IsPolicyRetired;
                    float outstandingNeed = Mathf.Max(0f,
                        policy.ResolveTarget(stock.Inventory) - stock.Inventory.GetOnHand(policy.resource));
                    float desiredUncovered = Mathf.Max(0f, outstandingNeed - current.Committed);
                    current.SetUncovered(desiredUncovered);
                    current.IsPolicyRetired = outstandingNeed <= QuantityEpsilon;
                    current.IsOpen = desiredUncovered > QuantityEpsilon ||
                                     current.Committed > QuantityEpsilon;
                    if (!wasPolicyRetired && current.IsPolicyRetired &&
                        current.Committed <= QuantityEpsilon)
                        Log("logistics.demand_target_satisfied", "Info", stock, null,
                            new SimulationLogField("demandId", current.Id),
                            new SimulationLogField("resource", current.Resource.name),
                            new SimulationLogField("retired", current.Retired));
                    CloseFulfilledOrders();
                    continue;
                }

                FreightOrder retiring = FindRetiringOrderWithCommitment(stock, policy.resource);
                if (retiring != null)
                {
                    retiring.LastRefreshTick = tick;
                    continue;
                }

                float onHand = stock.Inventory.GetOnHand(policy.resource);
                if (onHand > policy.reorderThreshold + QuantityEpsilon)
                    continue;

                float requested = Mathf.Max(0f, policy.ResolveTarget(stock.Inventory) - onHand);
                if (requested + QuantityEpsilon < policy.minimumShipmentQuantity)
                    continue;

                FreightOrder order = new FreightOrder(
                    "freight-order-" + (++nextOrderId).ToString("D6", CultureInfo.InvariantCulture),
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
            HandleUnavailableExecutors();
            ExpireUnpublishedOrders(tick);
            CloseFulfilledOrders();
            AssignAwaitingLegs();
            DispatchRoutineJobs();
            PruneTerminalHistory(jobs, job => job != null && job.IsTerminal);
            PruneTerminalHistory(orders, order => order != null && !order.IsOpen &&
                order.Committed <= QuantityEpsilon);
        }

        internal bool ReportExecution(
            FreightDeliveryJob job,
            IFreightLegExecution execution,
            FreightExecutionCorrelation correlation,
            FreightExecutionReport report,
            FreightFailureReason failureReason = FreightFailureReason.None,
            string detail = null)
        {
            if (!IsCurrentExecution(job, execution, correlation) || !execution.ProviderAvailable ||
                correlation.HasPartialPersonnelRoute)
                return false;

            switch (report)
            {
                case FreightExecutionReport.PickupRouteStarted:
                    if ((job.State != FreightJobState.Assigned && job.State != FreightJobState.Blocked) ||
                        job.HasPickedUp ||
                        !TryBeginPersonnelRoute(job, correlation))
                        return false;
                    TransitionJob(job, FreightJobState.TravelingToPickup);
                    return true;

                case FreightExecutionReport.ProviderAtSource:
                    if ((execution.RequiresPersonnelRouteForPickup &&
                         (job.State != FreightJobState.TravelingToPickup ||
                          !MatchesAndClearPersonnelRoute(job, correlation))) ||
                        (!execution.RequiresPersonnelRouteForPickup &&
                         job.State != FreightJobState.Assigned &&
                         (job.State != FreightJobState.Blocked || CurrentTick < job.RetryAtTick)) ||
                        job.HasPickedUp)
                        return false;
                    return PickupAtCurrentLeg(job);

                case FreightExecutionReport.LoadedRouteStarted:
                    if (!job.HasPickedUp ||
                        (job.State != FreightJobState.PickingUp && job.State != FreightJobState.Blocked) ||
                        !TryBeginPersonnelRoute(job, correlation))
                    {
                        return false;
                    }
                    job.SetRetryAtTick(jobMutationAuthority, 0L);
                    TransitionJob(job, FreightJobState.TravelingToDropoff);
                    return true;

                case FreightExecutionReport.LoadedLegArrived:
                    bool validLoadedArrival = execution.RequiresPersonnelRouteForLoadedArrival
                        ? job.State == FreightJobState.TravelingToDropoff &&
                          MatchesAndClearPersonnelRoute(job, correlation)
                        : job.State == FreightJobState.PickingUp || job.State == FreightJobState.Blocked;
                    if (!job.HasPickedUp || !validLoadedArrival ||
                        job.State == FreightJobState.Blocked && CurrentTick < job.RetryAtTick)
                        return false;
                    if (!execution.RequiresPersonnelRouteForLoadedArrival)
                        TransitionJob(job, FreightJobState.TravelingToDropoff);
                    return CompleteCurrentLeg(job);

                case FreightExecutionReport.Failed:
                    if (failureReason == FreightFailureReason.None)
                        return false;
                    if (!MatchesFailureRoute(job, correlation))
                        return false;
                    ClearPersonnelRoute(job);
                    if (job.HasPickedUp || job.CurrentLegIndex > 0 || HasPartialLegProgress(job))
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
            FreightExecutionCorrelation correlation,
            WorkReleaseReason reason)
        {
            if (!IsCurrentExecution(job, execution, correlation) || correlation.HasPartialPersonnelRoute ||
                !MatchesCurrentPersonnelRoute(job, correlation))
                return WorkReleaseDisposition.ReleasedNow;
            if (execution.DefersReleaseUntilAcceptedWorkCompletes)
                return WorkReleaseDisposition.Deferred;

            CancelBeforePickup(job, FreightFailureReason.WorkReleasedBeforePickup, reason.ToString());
            return WorkReleaseDisposition.ReleasedNow;
        }

        private bool IsCurrentExecution(
            FreightDeliveryJob job,
            IFreightLegExecution execution,
            FreightExecutionCorrelation correlation)
        {
            return IsKnown(job) && execution != null && execution.IsActive &&
                   !string.IsNullOrWhiteSpace(correlation.AllocationId) &&
                   !string.IsNullOrWhiteSpace(correlation.ExecutionId) &&
                   ReferenceEquals(job.ActiveLegExecution, execution) &&
                   job.CurrentLegIndex == execution.LegIndex && !job.IsTerminal &&
                   string.Equals(job.Allocation.Id, correlation.AllocationId, StringComparison.Ordinal) &&
                   correlation.LegIndex == job.CurrentLegIndex &&
                   string.Equals(execution.ExecutionId, correlation.ExecutionId, StringComparison.Ordinal);
        }

        private bool TryBeginPersonnelRoute(
            FreightDeliveryJob job,
            FreightExecutionCorrelation correlation)
        {
            if (!correlation.HasPersonnelRoute || !string.IsNullOrEmpty(job.ActivePersonnelRouteId))
                return false;
            job.SetActivePersonnelRoute(jobMutationAuthority,
                correlation.PersonnelRouteId, correlation.PersonnelRouteStableId);
            return true;
        }

        private bool MatchesCurrentPersonnelRoute(
            FreightDeliveryJob job,
            FreightExecutionCorrelation correlation)
        {
            if (string.IsNullOrEmpty(job.ActivePersonnelRouteId))
                return !correlation.HasPersonnelRoute;
            return correlation.HasPersonnelRoute &&
                   string.Equals(job.ActivePersonnelRouteId, correlation.PersonnelRouteId, StringComparison.Ordinal) &&
                   string.Equals(job.ActivePersonnelRouteStableId,
                       correlation.PersonnelRouteStableId, StringComparison.Ordinal);
        }

        private bool MatchesAndClearPersonnelRoute(
            FreightDeliveryJob job,
            FreightExecutionCorrelation correlation)
        {
            if (!correlation.HasPersonnelRoute ||
                !string.Equals(job.ActivePersonnelRouteId, correlation.PersonnelRouteId, StringComparison.Ordinal) ||
                !string.Equals(job.ActivePersonnelRouteStableId,
                    correlation.PersonnelRouteStableId, StringComparison.Ordinal))
            {
                return false;
            }

            ClearPersonnelRoute(job);
            return true;
        }

        private bool MatchesFailureRoute(
            FreightDeliveryJob job,
            FreightExecutionCorrelation correlation)
        {
            return MatchesCurrentPersonnelRoute(job, correlation);
        }

        private void ClearPersonnelRoute(FreightDeliveryJob job)
        {
            job.SetActivePersonnelRoute(jobMutationAuthority, null, null);
        }

        private void TransitionJob(
            FreightDeliveryJob job,
            FreightJobState state,
            FreightFailureReason reason = FreightFailureReason.None,
            string detail = null)
        {
            if (!IsKnown(job) || job.State == state)
                return;

            FreightJobState previousState = job.State;
            job.SetState(jobMutationAuthority, state);
            if (reason != FreightFailureReason.None)
                job.SetLastFailure(jobMutationAuthority, FailureCode(reason), detail);
            else if (previousState == FreightJobState.Blocked && state != FreightJobState.Blocked)
                job.SetLastFailure(jobMutationAuthority, string.Empty, string.Empty);
            IFreightLegExecution execution = job.ActiveLegExecution;
            string eventKey = state == FreightJobState.Blocked ? "logistics.job_blocked" :
                state == FreightJobState.Cancelled ? "logistics.job_cancelled" :
                "logistics.job_state_changed";
            FreightCustodySnapshot custody = job.Custody;
            Log(eventKey,
                state == FreightJobState.Blocked || state == FreightJobState.Cancelled ? "Warning" : "Info",
                execution != null ? execution.ProviderContext : null, job.CurrentLeg?.Destination,
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("legIndex", job.CurrentLegIndex),
                new SimulationLogField("executionId", execution != null ? execution.ExecutionId : string.Empty),
                new SimulationLogField("personnelRouteId", job.ActivePersonnelRouteId),
                new SimulationLogField("personnelRouteStableId", job.ActivePersonnelRouteStableId),
                new SimulationLogField("demandId", job.Order != null ? job.Order.Id : string.Empty),
                new SimulationLogField("state", state),
                new SimulationLogField("reason", FailureCode(reason)),
                new SimulationLogField("detail", detail ?? string.Empty),
                new SimulationLogField("quantity", job.Quantity),
                new SimulationLogField("originalQuantity", job.OriginalQuantity),
                new SimulationLogField("originReservedQuantity", custody.OriginReservedQuantity),
                new SimulationLogField("originInventory", custody.OriginInventory != null
                    ? SceneStableIdentity.GetKey(custody.OriginInventory) : string.Empty),
                new SimulationLogField("carriedQuantity", custody.CarriedQuantity),
                new SimulationLogField("carrierInventory", custody.CarrierInventory != null
                    ? SceneStableIdentity.GetKey(custody.CarrierInventory) : string.Empty),
                new SimulationLogField("stagedQuantity", custody.StagedQuantity),
                new SimulationLogField("stagingInventory", custody.StagingInventory != null
                    ? SceneStableIdentity.GetKey(custody.StagingInventory) : string.Empty),
                new SimulationLogField("deliveredQuantity", custody.DeliveredQuantity),
                new SimulationLogField("acceptedWorkerRemainder", job.AcceptedWorkerRemainder),
                new SimulationLogField("outstandingQuantity", custody.OutstandingQuantity),
                new SimulationLogField("custodyClaimLost", job.HasLostCustodyClaim),
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
                case FreightFailureReason.ExecutorPermanentlyUnavailable:
                    return "executor_permanently_unavailable";
                case FreightFailureReason.AcceptedCargoMissing: return "accepted_cargo_missing";
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

            IFreightLegExecution execution = job.ActiveLegExecution;
            bool completesAcrossLoads = execution.CompletesAcceptedQuantityAcrossLoads;
            FreightLegProgress progress = job.LegProgress;
            float requested;
            if (completesAcrossLoads)
            {
                if (progress == null || progress.LegIndex != job.CurrentLegIndex)
                {
                    progress = new FreightLegProgress(job.CurrentLegIndex, job.Quantity, job.Reservation);
                    job.SetLegProgress(jobMutationAuthority, progress);
                }

                if (progress.OriginReservation == null || !progress.OriginReservation.IsActive ||
                    progress.OriginReservation.Owner != sourceInventory ||
                    progress.OriginReservation.Remaining + QuantityEpsilon < progress.RemainingAtOrigin)
                {
                    CancelBeforePickup(job, FreightFailureReason.AllocationNoLongerFits);
                    return false;
                }

                requested = Mathf.Min(progress.RemainingAtOrigin,
                    Mathf.Min(execution.TripCapacity, cargoInventory.FreeCapacity));
                if (job.Resource.IsDiscrete)
                    requested = Mathf.Floor(requested + QuantityEpsilon);
                if (requested <= QuantityEpsilon)
                {
                    CancelBeforePickup(job, FreightFailureReason.DestinationOrCarrierUnavailable);
                    return false;
                }
            }
            else
            {
                requested = job.Quantity;
                if (job.Reservation.Remaining + QuantityEpsilon < requested ||
                    cargoInventory.FreeCapacity + QuantityEpsilon < requested ||
                    execution.TripCapacity + QuantityEpsilon < requested)
                {
                    CancelBeforePickup(job, FreightFailureReason.AllocationNoLongerFits);
                    return false;
                }
            }

            if (job.Reservation.Remaining + QuantityEpsilon < requested)
            {
                CancelBeforePickup(job, FreightFailureReason.AllocationNoLongerFits);
                return false;
            }

            InventoryComponent destinationInventory = leg.Destination.Inventory;
            float sourceOnHandBefore = sourceInventory.GetOnHand(job.Resource);
            float sourceReservedBefore = sourceInventory.GetReserved(job.Resource);
            float cargoOnHandBefore = cargoInventory.GetOnHand(job.Resource);
            float cargoReservedBefore = cargoInventory.GetReserved(job.Resource);
            float moved = sourceInventory.TransferOwnedToAndReserveDestination(
                job.Reservation, cargoInventory, requested, out InventoryReservationToken cargoReservation);
            if (moved <= QuantityEpsilon || cargoReservation == null ||
                !completesAcrossLoads && moved + QuantityEpsilon < requested)
            {
                CancelBeforePickup(job, FreightFailureReason.ReservedStockUnavailable);
                return false;
            }

            if (completesAcrossLoads)
            {
                progress.RemainingAtOrigin = Mathf.Max(0f, progress.RemainingAtOrigin - moved);
                progress.InCarrierQuantity += moved;
                progress.OriginReservation = job.Reservation.IsActive ? job.Reservation : null;
                progress.CurrentCarrierReservation = cargoReservation;
                progress.TripsStarted++;
            }

            job.SetReservation(jobMutationAuthority, cargoReservation);
            job.SetHasPickedUp(jobMutationAuthority, true);
            Log("logistics.pickup_completed", "Info", job.ActiveLegExecution.ProviderContext, leg.Origin,
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("legIndex", job.CurrentLegIndex),
                new SimulationLogField("executionId", job.ActiveLegExecution != null
                    ? job.ActiveLegExecution.ExecutionId : string.Empty),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("quantity", moved),
                new SimulationLogField("shipmentQuantity", job.Quantity),
                new SimulationLogField("tripCapacity", execution.TripCapacity),
                new SimulationLogField("tripNumber", progress != null ? progress.TripsStarted : 1),
                new SimulationLogField("remainingAtOrigin", progress != null
                    ? progress.RemainingAtOrigin : 0f),
                new SimulationLogField("inCarrier", progress != null
                    ? progress.InCarrierQuantity : moved),
                new SimulationLogField("sourceInventoryKey", SceneStableIdentity.GetKey(sourceInventory)),
                new SimulationLogField("carrierInventoryKey", SceneStableIdentity.GetKey(cargoInventory)),
                new SimulationLogField("sourceOnHandBefore", sourceOnHandBefore),
                new SimulationLogField("sourceReservedBefore", sourceReservedBefore),
                new SimulationLogField("sourceOnHandAfter", sourceInventory.GetOnHand(job.Resource)),
                new SimulationLogField("sourceReservedAfter", sourceInventory.GetReserved(job.Resource)),
                new SimulationLogField("carrierOnHandBefore", cargoOnHandBefore),
                new SimulationLogField("carrierReservedBefore", cargoReservedBefore),
                new SimulationLogField("carrierOnHandAfter", cargoInventory.GetOnHand(job.Resource)),
                new SimulationLogField("carrierReservedAfter", cargoInventory.GetReserved(job.Resource)),
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

            bool completesAcrossLoads = job.ActiveLegExecution.CompletesAcceptedQuantityAcrossLoads;
            FreightLegProgress progress = job.LegProgress;
            float requestedTransfer = completesAcrossLoads
                ? progress != null && progress.LegIndex == job.CurrentLegIndex
                    ? progress.InCarrierQuantity : 0f
                : job.Quantity;
            if (requestedTransfer <= QuantityEpsilon)
            {
                BlockJob(job, FreightFailureReason.DestinationOrCarrierUnavailable);
                return false;
            }

            float otherInbound = GetProjectedIncomingCapacity(destinationInventory, job);
            if (destinationInventory.FreeCapacity - otherInbound + QuantityEpsilon < requestedTransfer)
            {
                BlockJob(job, FreightFailureReason.DestinationHasNoCapacity);
                return false;
            }

            TransitionJob(job, FreightJobState.DroppingOff);
            int completedLegIndex = job.CurrentLegIndex;
            InventoryReservationToken carriedReservation = job.Reservation;
            InventoryReservationToken stagedReservation = null;
            ShuttleTransferEndpoint destinationEndpoint = FindEndpointForStock(leg.Destination);
            float sourceOnHandBefore = cargoInventory.GetOnHand(job.Resource);
            float sourceReservedBefore = cargoInventory.GetReserved(job.Resource);
            float sourceAvailableBefore = cargoInventory.GetAvailable(job.Resource);
            float destinationOnHandBefore = destinationInventory.GetOnHand(job.Resource);
            float destinationReservedBefore = destinationInventory.GetReserved(job.Resource);
            float destinationAvailableBefore = destinationInventory.GetAvailable(job.Resource);
            float moved = finalLeg
                ? cargoInventory.TransferOwnedTo(carriedReservation, destinationInventory, requestedTransfer)
                : cargoInventory.TransferOwnedToAndReserveDestination(
                    carriedReservation, destinationInventory, requestedTransfer, out stagedReservation);
            if (moved <= QuantityEpsilon)
            {
                Log("logistics.inventory_transfer_failed", "Warning", cargoInventory, destinationInventory,
                    new SimulationLogField("allocationId", job.Allocation.Id),
                    new SimulationLogField("demandId", job.Order.Id),
                    new SimulationLogField("legIndex", completedLegIndex),
                    new SimulationLogField("resource", job.Resource.name),
                    new SimulationLogField("requested", requestedTransfer),
                    new SimulationLogField("sourceInventoryKey", SceneStableIdentity.GetKey(cargoInventory)),
                    new SimulationLogField("destinationInventoryKey", SceneStableIdentity.GetKey(destinationInventory)),
                    new SimulationLogField("destinationStock", leg.Destination.name),
                    new SimulationLogField("destinationEndpoint", destinationEndpoint != null
                        ? destinationEndpoint.StableId : string.Empty),
                    new SimulationLogField("endpointInventoryKey", destinationEndpoint != null &&
                        destinationEndpoint.StagingInventory != null
                            ? SceneStableIdentity.GetKey(destinationEndpoint.StagingInventory) : string.Empty),
                    new SimulationLogField("endpointInventoryMatchesDestination",
                        destinationEndpoint != null && destinationEndpoint.StagingInventory == destinationInventory),
                    new SimulationLogField("sourceOnHandBefore", sourceOnHandBefore),
                    new SimulationLogField("sourceReservedBefore", sourceReservedBefore),
                    new SimulationLogField("sourceAvailableBefore", sourceAvailableBefore),
                    new SimulationLogField("destinationOnHandBefore", destinationOnHandBefore),
                    new SimulationLogField("destinationReservedBefore", destinationReservedBefore),
                    new SimulationLogField("destinationAvailableBefore", destinationAvailableBefore),
                    new SimulationLogField("carriedReservationActive", carriedReservation != null &&
                        carriedReservation.IsActive),
                    new SimulationLogField("carriedReservationOwnerMatchesSource",
                        carriedReservation != null && carriedReservation.Owner == cargoInventory));
                BlockJob(job, FreightFailureReason.DestinationHasNoCapacity);
                return false;
            }

            Log("logistics.inventory_transfer", "Info", cargoInventory, destinationInventory,
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("demandId", job.Order.Id),
                new SimulationLogField("legIndex", completedLegIndex),
                new SimulationLogField("resource", job.Resource.name),
                new SimulationLogField("requested", requestedTransfer),
                new SimulationLogField("transferred", moved),
                new SimulationLogField("transferKind", finalLeg ? "final_delivery" :
                    leg.Type == LogisticsRouteLegType.ShuttleFreight ? "shuttle_staging" : "staging"),
                new SimulationLogField("sourceInventoryKey", SceneStableIdentity.GetKey(cargoInventory)),
                new SimulationLogField("destinationInventoryKey", SceneStableIdentity.GetKey(destinationInventory)),
                new SimulationLogField("destinationStock", leg.Destination.name),
                new SimulationLogField("destinationEndpoint", destinationEndpoint != null
                    ? destinationEndpoint.StableId : string.Empty),
                new SimulationLogField("endpointInventoryKey", destinationEndpoint != null &&
                    destinationEndpoint.StagingInventory != null
                        ? SceneStableIdentity.GetKey(destinationEndpoint.StagingInventory) : string.Empty),
                new SimulationLogField("endpointInventoryMatchesDestination",
                    destinationEndpoint != null && destinationEndpoint.StagingInventory == destinationInventory),
                new SimulationLogField("sourceOnHandBefore", sourceOnHandBefore),
                new SimulationLogField("sourceReservedBefore", sourceReservedBefore),
                new SimulationLogField("sourceAvailableBefore", sourceAvailableBefore),
                new SimulationLogField("sourceOnHandAfter", cargoInventory.GetOnHand(job.Resource)),
                new SimulationLogField("sourceReservedAfter", cargoInventory.GetReserved(job.Resource)),
                new SimulationLogField("sourceAvailableAfter", cargoInventory.GetAvailable(job.Resource)),
                new SimulationLogField("destinationOnHandBefore", destinationOnHandBefore),
                new SimulationLogField("destinationReservedBefore", destinationReservedBefore),
                new SimulationLogField("destinationAvailableBefore", destinationAvailableBefore),
                new SimulationLogField("destinationOnHandAfter", destinationInventory.GetOnHand(job.Resource)),
                new SimulationLogField("destinationReservedAfter", destinationInventory.GetReserved(job.Resource)),
                new SimulationLogField("destinationAvailableAfter", destinationInventory.GetAvailable(job.Resource)),
                new SimulationLogField("destinationReservationCreated", stagedReservation != null &&
                    stagedReservation.IsActive),
                new SimulationLogField("carriedReservationActive", carriedReservation != null &&
                    carriedReservation.IsActive),
                new SimulationLogField("carriedReservationOwnerMatchesSource",
                    carriedReservation != null && carriedReservation.Owner == cargoInventory));

            if (completesAcrossLoads)
            {
                if (!finalLeg &&
                    (moved + QuantityEpsilon < requestedTransfer || stagedReservation == null))
                {
                    BlockJob(job, FreightFailureReason.StageReservationFailed);
                    return false;
                }

                progress.InCarrierQuantity = Mathf.Max(0f, progress.InCarrierQuantity - moved);
                progress.CurrentCarrierReservation = carriedReservation.IsActive ? carriedReservation : null;
                if (!finalLeg)
                {
                    progress.ArrivedAtDestination += moved;
                    if (progress.DestinationReservation == null)
                        progress.DestinationReservation = stagedReservation;
                    else if (!destinationInventory.MergeOwnedReservations(
                                 progress.DestinationReservation, stagedReservation))
                        progress.AdditionalDestinationReservations.Add(stagedReservation);
                }

                if (finalLeg)
                {
                    RecordFinalDelivery(job, moved);
                    if (moved + QuantityEpsilon < requestedTransfer)
                    {
                        float liveCarrierClaim = carriedReservation.IsActive
                            ? carriedReservation.Remaining : 0f;
                        float missingCargo = Mathf.Max(0f,
                            progress.InCarrierQuantity - liveCarrierClaim);
                        job.SetReservation(jobMutationAuthority,
                            carriedReservation.IsActive ? carriedReservation : null);
                        if (missingCargo > QuantityEpsilon)
                        {
                            progress.InCarrierQuantity = liveCarrierClaim;
                            progress.CurrentCarrierReservation = carriedReservation.IsActive
                                ? carriedReservation : null;
                            BlockJob(job, FreightFailureReason.AcceptedCargoMissing,
                                "final transfer moved " + moved.ToString(CultureInfo.InvariantCulture) +
                                " of " + requestedTransfer.ToString(CultureInfo.InvariantCulture) +
                                "; missing accepted cargo=" +
                                missingCargo.ToString(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            BlockJob(job, FreightFailureReason.DestinationHasNoCapacity,
                                "partial final transfer; live carrier claim is retained for retry");
                        }
                        return false;
                    }
                    Log("logistics.delivery_completed", "Info", job.ActiveLegExecution.ProviderContext, leg.Destination,
                        new SimulationLogField("allocationId", job.Allocation.Id),
                        new SimulationLogField("legIndex", completedLegIndex),
                        new SimulationLogField("executionId", job.ActiveLegExecution.ExecutionId),
                        new SimulationLogField("currentLegIndex", completedLegIndex),
                        new SimulationLogField("resource", job.Resource.name),
                        new SimulationLogField("quantity", moved),
                        new SimulationLogField("remainingShipmentQuantity", job.Quantity),
                        new SimulationLogField("demandId", job.Order.Id));
                }

                job.SetHasPickedUp(jobMutationAuthority, false);
                if (progress.RemainingAtOrigin > QuantityEpsilon)
                {
                    if (progress.OriginReservation == null || !progress.OriginReservation.IsActive)
                    {
                        BlockJob(job, FreightFailureReason.ReservedStockUnavailable);
                        return false;
                    }
                    job.SetReservation(jobMutationAuthority, progress.OriginReservation);
                    TransitionJob(job, FreightJobState.Assigned);
                    Log("logistics.walking_trip_completed", "Info",
                        job.ActiveLegExecution.ProviderContext, leg.Destination,
                        new SimulationLogField("allocationId", job.Allocation.Id),
                        new SimulationLogField("legIndex", completedLegIndex),
                        new SimulationLogField("tripNumber", progress.TripsStarted),
                        new SimulationLogField("quantity", moved),
                        new SimulationLogField("remainingAtOrigin", progress.RemainingAtOrigin),
                        new SimulationLogField("arrivedAtDestination", progress.ArrivedAtDestination),
                        new SimulationLogField("demandId", job.Order.Id));
                    return true;
                }

                if (progress.OriginReservation != null && progress.OriginReservation.IsActive)
                    leg.Origin.Inventory.ReleaseOwned(progress.OriginReservation);

                if (finalLeg)
                {
                    if (job.Quantity > QuantityEpsilon)
                    {
                        BlockJob(job, FreightFailureReason.CarrierRetainsUnacceptedCargo);
                        return false;
                    }

                    job.SetReservation(jobMutationAuthority, null);
                    job.SetLegProgress(jobMutationAuthority, null);
                    TransitionJob(job, FreightJobState.Completed);
                    CloseFulfilledOrders();
                    FinishExecution(job);
                    job.SetActiveLegExecution(jobMutationAuthority, null);
                    return true;
                }

                if (!TryMergeDestinationReservations(destinationInventory, progress) ||
                    progress.DestinationReservation == null ||
                    progress.DestinationReservation.Remaining + QuantityEpsilon < progress.TotalQuantity)
                {
                    BlockJob(job, FreightFailureReason.StageReservationFailed);
                    return false;
                }

                job.SetReservation(jobMutationAuthority, progress.DestinationReservation);
                job.SetLegProgress(jobMutationAuthority, null);
                if (!job.AdvanceLeg(jobMutationAuthority))
                {
                    BlockJob(job, FreightFailureReason.StageReservationFailed);
                    return false;
                }

                TransitionJob(job, FreightJobState.Assigned);
                Log("logistics.leg_staged", "Info", job.ActiveLegExecution.ProviderContext, leg.Destination,
                    new SimulationLogField("allocationId", job.Allocation.Id),
                    new SimulationLogField("legIndex", completedLegIndex),
                    new SimulationLogField("executionId", job.ActiveLegExecution.ExecutionId),
                    new SimulationLogField("completedLegIndex", completedLegIndex),
                    new SimulationLogField("nextLegIndex", job.CurrentLegIndex),
                    new SimulationLogField("resource", job.Resource.name),
                    new SimulationLogField("quantity", progress.ArrivedAtDestination),
                    new SimulationLogField("stagedReservation", progress.DestinationReservation.Remaining),
                    new SimulationLogField("demandId", job.Order.Id));
                FinishExecution(job);
                job.SetActiveLegExecution(jobMutationAuthority, null);
                return true;
            }

            if (!finalLeg &&
                (moved + QuantityEpsilon < requestedTransfer || stagedReservation == null))
            {
                BlockJob(job, FreightFailureReason.StageReservationFailed);
                return false;
            }

            if (finalLeg)
            {
                job.SetReservation(jobMutationAuthority, carriedReservation.IsActive ? carriedReservation : null);
                RecordFinalDelivery(job, moved);
                if (job.Quantity > QuantityEpsilon)
                {
                    float liveCarrierClaim = carriedReservation.IsActive
                        ? carriedReservation.Remaining : 0f;
                    BlockJob(job, liveCarrierClaim + QuantityEpsilon < job.Quantity
                        ? FreightFailureReason.AcceptedCargoMissing
                        : FreightFailureReason.DestinationHasNoCapacity,
                        "final transfer moved " + moved.ToString(CultureInfo.InvariantCulture) +
                        " of " + requestedTransfer.ToString(CultureInfo.InvariantCulture) +
                        "; remaining obligation=" + job.Quantity.ToString(CultureInfo.InvariantCulture) +
                        ", live carrier claim=" + liveCarrierClaim.ToString(CultureInfo.InvariantCulture));
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

            job.SetReservation(jobMutationAuthority, stagedReservation);
            job.SetHasPickedUp(jobMutationAuthority, false);
            if (!job.AdvanceLeg(jobMutationAuthority))
            {
                BlockJob(job, FreightFailureReason.StageReservationFailed);
                return false;
            }
            TransitionJob(job, FreightJobState.Assigned);
            FinishExecution(job);
            job.SetActiveLegExecution(jobMutationAuthority, null);
            return true;
        }

        private static bool TryMergeDestinationReservations(
            InventoryComponent destination,
            FreightLegProgress progress)
        {
            for (int index = 0; index < progress.AdditionalDestinationReservations.Count; index++)
            {
                InventoryReservationToken token = progress.AdditionalDestinationReservations[index];
                if (token == null || !token.IsActive)
                    continue;
                if (progress.DestinationReservation == null)
                {
                    progress.DestinationReservation = token;
                    continue;
                }
                if (!destination.MergeOwnedReservations(progress.DestinationReservation, token))
                    return false;
            }
            progress.AdditionalDestinationReservations.Clear();
            return true;
        }
        private void CancelBeforePickup(
            FreightDeliveryJob job,
            FreightFailureReason reason,
            string detail = null)
        {
            if (!IsKnown(job) || job.HasPickedUp || job.IsTerminal)
                return;

            if (job.CurrentLegIndex > 0 || HasPartialLegProgress(job))
            {
                BlockJob(job, reason, detail);
                return;
            }

            job.CurrentLeg?.Origin?.Inventory?.ReleaseOwned(job.Reservation);
            ReleaseOutstandingCommitment(job);
            job.SetReservation(jobMutationAuthority, null);
            job.SetLegProgress(jobMutationAuthority, null);
            ClearPersonnelRoute(job);
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
                reason == FreightFailureReason.OrderFulfilledCarrierRetainsExcessCargo ||
                reason == FreightFailureReason.ExecutorPermanentlyUnavailable ||
                reason == FreightFailureReason.AcceptedCargoMissing
                    ? long.MaxValue
                    : CurrentTick + BlockedRetryTicks);
            TransitionJob(job, FreightJobState.Blocked, reason, detail);
        }

        private void HandleUnavailableExecutors()
        {
            for (int index = 0; index < jobs.Count; index++)
            {
                FreightDeliveryJob job = jobs[index];
                IFreightLegExecution execution = job != null ? job.ActiveLegExecution : null;
                if (job == null || job.IsTerminal || execution == null || execution.ExecutorAvailable ||
                    (job.State == FreightJobState.Blocked && job.RetryAtTick == long.MaxValue))
                    continue;

                if (!job.HasPickedUp && job.CurrentLegIndex == 0 && job.LegProgress == null)
                {
                    CancelBeforePickup(job, FreightFailureReason.ExecutorPermanentlyUnavailable,
                        "executor disappeared before pickup; original source claim released to active demand");
                }
                else
                {
                    BlockJob(job, FreightFailureReason.ExecutorPermanentlyUnavailable,
                        "executor disappeared with a live obligation; custody is retained when its inventory survives, manual recovery required");
                }
            }
        }

        private void DispatchRoutineJobs()
        {
            List<FreightOrder> pending = GetDispatchOrderPriority();
            for (int i = 0; i < pending.Count; i++)
            {
                FreightOrder order = pending[i];
                while (order.IsOpen && order.Uncovered > QuantityEpsilon)
                {
                    float before = order.Uncovered;
                    Candidate best = FindBestCandidate(order, before);
                    if (best == null || !AcceptCandidate(order, best))
                        break;
                    if (before - order.Uncovered <= QuantityEpsilon)
                        break;
                }
            }
        }

        /// <summary>Stops future dispatch for a publisher while accepted children retain custody.</summary>
        public void StopPublishing(LogisticsStockComponent stock, ResourceDefinition resource = null)
        {
            if (stock == null)
                return;
            for (int index = 0; index < orders.Count; index++)
            {
                FreightOrder order = orders[index];
                if (order == null || order.Requester != stock ||
                    (resource != null && order.Resource != resource) || !order.IsOpen)
                    continue;
                RetireDemand(order);
            }
        }

        /// <summary>Explicitly retires the unassigned remainder; accepted child jobs finish.</summary>
        public bool TryCancelDemand(FreightOrder order)
        {
            if (order == null || !orders.Contains(order) || !order.IsOpen)
                return false;
            RetireDemand(order);
            return true;
        }

        /// <summary>Reopens a child only while it remains untouched at its original source.</summary>
        public bool TryCancelUnacceptedChild(FreightDeliveryJob job)
        {
            if (!IsKnown(job) || job.IsTerminal || job.HasPickedUp ||
                job.CurrentLegIndex != 0 || job.LegProgress != null)
                return false;
            CancelBeforePickup(job, FreightFailureReason.WorkReleasedBeforePickup,
                "explicitly cancelled before physical execution");
            return job.IsTerminal;
        }

        internal bool HasLiveJobsForStock(LogisticsStockComponent stock)
        {
            if (stock == null)
                return false;
            for (int index = 0; index < jobs.Count; index++)
            {
                FreightDeliveryJob job = jobs[index];
                if (job == null || job.IsTerminal)
                    continue;
                if (job.Reservation != null && job.Reservation.IsActive &&
                    job.Reservation.Owner == stock.Inventory)
                    return true;
                IReadOnlyList<LogisticsRouteLeg> legs = job.RoutePlan.Legs;
                for (int legIndex = 0; legIndex < legs.Count; legIndex++)
                    if (legs[legIndex].Origin == stock || legs[legIndex].Destination == stock)
                        return true;
            }
            return false;
        }

        private void RetireDemand(FreightOrder order)
        {
            if (order.Delivered + QuantityEpsilon >= order.Requested &&
                order.Committed <= QuantityEpsilon)
            {
                order.IsOpen = false;
                order.IsPolicyRetired = false;
                return;
            }
            order.RetireUncovered();
            order.IsOpen = false;
            order.IsRetiring = true;
            order.IsPolicyRetired = false;
            Log("logistics.demand_retiring", "Info", order.Requester, null,
                new SimulationLogField("demandId", order.Id),
                new SimulationLogField("resource", order.Resource.name),
                new SimulationLogField("delivered", order.Delivered),
                new SimulationLogField("committed", order.Committed),
                new SimulationLogField("retired", order.Retired));
        }

        private void AssignAwaitingLegs()
        {
            for (int index = 0; index < jobs.Count; index++)
            {
                FreightDeliveryJob job = jobs[index];
                LogisticsRouteLeg leg = job != null ? job.CurrentLeg : null;
                if (job == null || !job.IsAwaitingLegAssignment || leg == null)
                    continue;

                if (leg.Type == LogisticsRouteLegType.ShuttleFreight)
                {
                    AssignShuttleLeg(job, leg);
                    continue;
                }

                if (leg.Type == LogisticsRouteLegType.WalkingCarrier)
                    AssignWalkingLeg(job, leg);
            }
        }

        private static ShuttleTransferEndpoint FindEndpointForStock(LogisticsStockComponent stock)
        {
            if (stock == null)
                return null;

            IReadOnlyList<ShuttleTransferEndpoint> endpoints = ShuttleTransferEndpoint.Active;
            for (int index = 0; index < endpoints.Count; index++)
            {
                ShuttleTransferEndpoint endpoint = endpoints[index];
                if (endpoint != null && endpoint.DepotStock == stock)
                    return endpoint;
            }

            return null;
        }

        private void AssignShuttleLeg(FreightDeliveryJob job, LogisticsRouteLeg leg)
        {
            ShuttleManager shuttleManager = ShuttleManager.Instance;
            if (shuttleManager == null ||
                !shuttleManager.TryQuoteFreightLeg(leg, job.Quantity, out ShuttleFreightQuote quote))
                return;

            string executionId = "freight-execution-" +
                (++nextExecutionId).ToString("D6", CultureInfo.InvariantCulture);
            if (!((IFreightLegProvider)shuttleManager).TryAcceptQuote(
                    quote, job, executionId, out IFreightLegExecution execution))
            {
                return;
            }
            job.SetActiveLegExecution(jobMutationAuthority, execution);
        }

        private void AssignWalkingLeg(FreightDeliveryJob job, LogisticsRouteLeg leg)
        {
            Candidate candidate = FindWalkingLegCandidate(job, leg, FreightWorkPurpose.RoutinePorter);
            if (candidate == null && job.CurrentLegIndex == 0 &&
                leg.Origin.TryGetPolicy(job.Resource, out LogisticsStockPolicyEntry sourcePolicy) &&
                sourcePolicy.role == LogisticsStockRole.Producer)
            {
                candidate = FindWalkingLegCandidate(job, leg,
                    FreightWorkPurpose.ProducerOutboundAssist);
            }

            bool finalLeg = job.CurrentLegIndex == job.RoutePlan.Legs.Count - 1;
            if (candidate == null && finalLeg && ShouldUseConsumerEmergencyPickup(job))
                candidate = FindWalkingLegCandidate(job, leg,
                    FreightWorkPurpose.ConsumerEmergencyPickup);

            if (candidate == null)
                return;

            string executionId = "freight-execution-" +
                (++nextExecutionId).ToString("D6", CultureInfo.InvariantCulture);
            if (candidate.Provider == null || candidate.LegQuote == null ||
                !candidate.Provider.TryAcceptQuote(
                    candidate.LegQuote, job, executionId, out IFreightLegExecution execution))
            {
                return;
            }
            job.SetActiveLegExecution(jobMutationAuthority, execution);
        }

        private Candidate FindWalkingLegCandidate(
            FreightDeliveryJob job,
            LogisticsRouteLeg leg,
            FreightWorkPurpose purpose)
        {
            if (job == null || leg == null || leg.Origin == null || leg.Destination == null)
                return null;
            if (!TryEstimateWalkingLeg(leg.Origin.FreightAnchor,
                    leg.Destination.FreightAnchor,
                    out PersonnelRouteEstimate loadedEstimate))
                return null;

            Candidate best = null;
            IReadOnlyList<WalkingFreightWorkService> services = WalkingFreightWorkService.Active;
            for (int serviceIndex = 0; serviceIndex < services.Count; serviceIndex++)
            {
                WalkingFreightWorkService service = services[serviceIndex];
                if (service == null || !service.isActiveAndEnabled ||
                    !service.TryQuote(leg.Origin, leg.Destination, job.Destination,
                        job.Resource, job.Quantity,
                        purpose, loadedEstimate, out FreightWorkQuote quote))
                    continue;
                Candidate candidate = new Candidate(service, quote, leg.Origin, job.Quantity,
                    service.GetStableKey() + "/" + leg.Origin.GetStableKey() + "/" + job.Allocation.Id);
                if (best == null || Candidate.Compare(candidate, best) < 0)
                    best = candidate;
            }
            return best;
        }

        private Candidate FindBestCandidate(FreightOrder order, float dispatchable)
        {
            Candidate bestShuttle = null;
            Candidate bestRoutine = null;
            Candidate bestProducerAssist = null;
            Candidate bestEmergency = null;
            IReadOnlyList<WalkingFreightWorkService> services = WalkingFreightWorkService.Active;
            IReadOnlyList<LogisticsStockComponent> supplies = LogisticsStockComponent.Active;
            float destinationSpace = order.Requester.Inventory.FreeCapacity -
                                     GetProjectedIncomingCapacity(order.Requester.Inventory, null);
            float shipmentLimit = Mathf.Min(dispatchable, Mathf.Max(0f, destinationSpace));
            if (order.Resource.IsDiscrete)
                shipmentLimit = Mathf.Floor(shipmentLimit + QuantityEpsilon);
            if (shipmentLimit <= QuantityEpsilon)
                return null;

            for (int sourceIndex = 0; sourceIndex < supplies.Count; sourceIndex++)
            {
                LogisticsStockComponent source = supplies[sourceIndex];
                if (source == null || source == order.Requester || source.Inventory == null ||
                    !source.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry supplyPolicy) ||
                    supplyPolicy.role == LogisticsStockRole.Consumer)
                    continue;

                float available = source.Inventory.GetAvailable(order.Resource);
                float desiredQuantity = Mathf.Min(shipmentLimit, available);
                if (order.Resource.IsDiscrete)
                    desiredQuantity = Mathf.Floor(desiredQuantity + QuantityEpsilon);
                if (desiredQuantity <= QuantityEpsilon)
                    continue;

                Candidate shuttleLeading = FindShuttleLeadingCandidate(order, source, desiredQuantity);
                if (shuttleLeading != null &&
                    (bestShuttle == null || Candidate.Compare(shuttleLeading, bestShuttle) < 0))
                    bestShuttle = shuttleLeading;

                bool emergencyDemand = ShouldUseConsumerEmergencyPickup(order);
                FreightWorkPurpose[] purposes = supplyPolicy.role == LogisticsStockRole.Producer
                    ? emergencyDemand
                        ? new[] { FreightWorkPurpose.RoutinePorter,
                            FreightWorkPurpose.ProducerOutboundAssist,
                            FreightWorkPurpose.ConsumerEmergencyPickup }
                        : new[] { FreightWorkPurpose.RoutinePorter,
                            FreightWorkPurpose.ProducerOutboundAssist }
                    : emergencyDemand
                        ? new[] { FreightWorkPurpose.RoutinePorter,
                            FreightWorkPurpose.ConsumerEmergencyPickup }
                        : new[] { FreightWorkPurpose.RoutinePorter };
                bool hasDirectWalkingRoute = TryEstimateWalkingLeg(
                    source.FreightAnchor, order.Requester.FreightAnchor,
                    out PersonnelRouteEstimate directEstimate, out string directReason);
                if (!hasDirectWalkingRoute)
                    LogWalkingLegUnavailable(order, source, order.Requester, directReason);

                for (int purposeIndex = 0; purposeIndex < purposes.Length; purposeIndex++)
                {
                    FreightWorkPurpose purpose = purposes[purposeIndex];
                    Candidate bestForPurpose = null;
                    if (hasDirectWalkingRoute)
                    {
                        FreightWorkQuote directQuote = FindBestServiceQuote(services,
                            source, order.Requester, order.Requester,
                            order.Resource, directEstimate, desiredQuantity, purpose);
                        if (directQuote != null)
                        {
                            LogisticsRoutePlan directPlan = new LogisticsRoutePlan(order,
                                new[] { new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                                    source, order.Requester, directEstimate.Distance) });
                            bestForPurpose = new Candidate(directQuote.Provider, directQuote, source,
                                desiredQuantity,
                                directQuote.Provider.GetStableKey() + "/" + source.GetStableKey() + "/" + order.Id,
                                directPlan);
                        }
                    }

                    Candidate multimodal = FindBestMultimodalCandidate(order, source,
                        desiredQuantity, purpose, services);
                    if (multimodal != null &&
                        (bestForPurpose == null || Candidate.Compare(multimodal, bestForPurpose) < 0))
                        bestForPurpose = multimodal;

                    if (bestForPurpose != null)
                    {
                        if (purpose == FreightWorkPurpose.RoutinePorter &&
                            (bestRoutine == null || Candidate.Compare(bestForPurpose, bestRoutine) < 0))
                            bestRoutine = bestForPurpose;
                        else if (purpose == FreightWorkPurpose.ProducerOutboundAssist &&
                                 (bestProducerAssist == null ||
                                  Candidate.Compare(bestForPurpose, bestProducerAssist) < 0))
                            bestProducerAssist = bestForPurpose;
                        else if (purpose == FreightWorkPurpose.ConsumerEmergencyPickup &&
                                 (bestEmergency == null ||
                                  Candidate.Compare(bestForPurpose, bestEmergency) < 0))
                            bestEmergency = bestForPurpose;
                    }
                }
            }

            Candidate best = bestShuttle;
            if (bestRoutine != null && (best == null || Candidate.Compare(bestRoutine, best) < 0))
                best = bestRoutine;
            if (best != null)
                return best;
            if (bestProducerAssist != null)
                return bestProducerAssist;
            return bestEmergency;
        }

        private Candidate FindBestMultimodalCandidate(
            FreightOrder order,
            LogisticsStockComponent source,
            float desiredQuantity,
            FreightWorkPurpose purpose,
            IReadOnlyList<WalkingFreightWorkService> services)
        {
            Candidate best = null;
            ShuttleManager shuttleManager = ShuttleManager.Instance;
            PersonnelRoutingManager routing = PersonnelRoutingManager.Instance;
            if (shuttleManager == null || routing == null)
                return null;

            float shuttleCapacity = GetMaximumShuttleCargoCapacity(shuttleManager);
            if (shuttleCapacity <= QuantityEpsilon)
                return null;

            IReadOnlyList<ShuttleTransferEndpoint> endpoints = shuttleManager.Endpoints;
            ShuttleTransferEndpoint nearestOrigin = FindNearestOriginEndpoint(
                order, source, order.Resource, endpoints,
                out PersonnelRouteEstimate originWalkingEstimate);
            if (nearestOrigin == null)
                return null;

            for (int originIndex = 0; originIndex < endpoints.Count; originIndex++)
            {
                ShuttleTransferEndpoint origin = endpoints[originIndex];
                if (origin != nearestOrigin)
                    continue;
                LogisticsStockComponent originStock = origin != null ? origin.DepotStock : null;
                if (originStock == null || originStock == source || originStock.Inventory == null ||
                    origin.StagingInventory != originStock.Inventory || origin.TransferAnchor == null ||
                    !originStock.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry originPolicy) ||
                    originPolicy.role != LogisticsStockRole.Depot ||
                    !TryEstimateWalkingLeg(source.FreightAnchor, origin.TransferAnchor, out _))
                    continue;

                float originSpace = originStock.Inventory.FreeCapacity -
                                    GetProjectedIncomingCapacity(originStock.Inventory, null);
                float maximumAtOrigin = Mathf.Min(desiredQuantity, Mathf.Max(0f, originSpace));
                if (maximumAtOrigin <= QuantityEpsilon)
                    continue;

                ShuttleTransferEndpoint nearestDestination = FindNearestDestinationEndpoint(
                    order, source, origin, order.Resource, endpoints);
                if (nearestDestination == null)
                    continue;

                for (int destinationIndex = 0; destinationIndex < endpoints.Count; destinationIndex++)
                {
                    ShuttleTransferEndpoint destination = endpoints[destinationIndex];
                    if (destination != nearestDestination)
                        continue;
                    LogisticsStockComponent destinationStock = destination != null
                        ? destination.DepotStock : null;
                    if (destination == null || destination == origin || destinationStock == null ||
                        destinationStock.Inventory == null ||
                        destination.StagingInventory != destinationStock.Inventory ||
                        !shuttleManager.CanService(origin, destination, ShuttlePayloadType.Freight) ||
                        destination.TransferAnchor == null ||
                        destinationStock == source ||
                        !destinationStock.TryGetPolicy(order.Resource,
                            out LogisticsStockPolicyEntry destinationPolicy) ||
                        destinationStock != order.Requester &&
                            destinationPolicy.role != LogisticsStockRole.Depot)
                        continue;

                    float destinationDepotSpace = destinationStock.Inventory.FreeCapacity -
                        GetProjectedIncomingCapacity(destinationStock.Inventory, null);
                    float routeQuantity = FreightShipmentSizing.LimitToCapacity(
                        desiredQuantity, maximumAtOrigin, destinationDepotSpace,
                        shuttleCapacity, order.Resource.IsDiscrete);
                    if (routeQuantity <= QuantityEpsilon)
                        continue;

                    PersonnelRouteEstimate finalWalkingEstimate = default;
                    if (destinationStock != order.Requester &&
                        !TryEstimateWalkingLeg(destination.TransferAnchor,
                            order.Requester.FreightAnchor,
                            out finalWalkingEstimate))
                        continue;

                FreightWorkQuote firstQuote = FindBestServiceQuote(services,
                    source, originStock, order.Requester,
                    order.Resource, originWalkingEstimate, routeQuantity, purpose);
                    if (firstQuote == null)
                        continue;

                    float flightDistance = Vector3.Distance(origin.TransferAnchor.position,
                        destination.TransferAnchor.position);
                    LogisticsRoutePlan route;
                    float remainingCost = flightDistance;
                    if (destinationStock == order.Requester)
                    {
                        if (destination.StagingInventory != order.Requester.Inventory)
                            continue;
                        route = new LogisticsRoutePlan(order, new[]
                        {
                            new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                                source, originStock, firstQuote.LoadedCargoTravelCost),
                            LogisticsRouteLeg.Shuttle(originStock, order.Requester,
                                origin, destination, flightDistance)
                        });
                    }
                    else
                    {
                        remainingCost += finalWalkingEstimate.Distance;
                        route = new LogisticsRoutePlan(order, new[]
                        {
                            new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                                source, originStock, firstQuote.LoadedCargoTravelCost),
                            LogisticsRouteLeg.Shuttle(originStock, destinationStock,
                                origin, destination, flightDistance),
                            new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                                destinationStock, order.Requester, finalWalkingEstimate.Distance)
                        });
                    }
                    Candidate candidate = new Candidate(firstQuote.Provider, firstQuote, source,
                        routeQuantity,
                        firstQuote.Provider.GetStableKey() + "/" + source.GetStableKey() + "/" + order.Id,
                        route,
                        firstQuote.TotalServiceCost + remainingCost);
                    if (best == null || Candidate.Compare(candidate, best) < 0)
                        best = candidate;
                }
            }
            return best;
        }

        private static ShuttleTransferEndpoint FindNearestOriginEndpoint(
            FreightOrder order,
            LogisticsStockComponent source,
            ResourceDefinition resource,
            IReadOnlyList<ShuttleTransferEndpoint> endpoints,
            out PersonnelRouteEstimate nearestEstimate)
        {
            nearestEstimate = default;
            ShuttleTransferEndpoint best = null;
            float bestDistance = float.PositiveInfinity;
            string bestKey = string.Empty;
            for (int index = 0; index < endpoints.Count; index++)
            {
                ShuttleTransferEndpoint candidate = endpoints[index];
                LogisticsStockComponent stock = candidate != null ? candidate.DepotStock : null;
                if (stock == null || stock == source || stock == order.Requester ||
                    stock.Inventory == null || candidate.StagingInventory != stock.Inventory ||
                    candidate.TransferAnchor == null ||
                    !stock.TryGetPolicy(resource, out LogisticsStockPolicyEntry policy) ||
                    policy.role != LogisticsStockRole.Depot ||
                    !TryEstimateWalkingLeg(source.FreightAnchor, candidate.TransferAnchor,
                        out PersonnelRouteEstimate estimate))
                    continue;

                string stableKey = candidate.StableId ?? string.Empty;
                if (estimate.Distance < bestDistance - QuantityEpsilon ||
                    Mathf.Abs(estimate.Distance - bestDistance) <= QuantityEpsilon &&
                    string.CompareOrdinal(stableKey, bestKey) < 0)
                {
                    best = candidate;
                    bestDistance = estimate.Distance;
                    bestKey = stableKey;
                    nearestEstimate = estimate;
                }
            }
            return best;
        }

        private static ShuttleTransferEndpoint FindNearestDestinationEndpoint(
            FreightOrder order,
            LogisticsStockComponent source,
            ShuttleTransferEndpoint origin,
            ResourceDefinition resource,
            IReadOnlyList<ShuttleTransferEndpoint> endpoints)
        {
            // If the consumer itself is a registered dock, landing there is the
            // shortest final handoff and needs no porter leg.
            ShuttleTransferEndpoint directConsumer = null;
            for (int index = 0; index < endpoints.Count; index++)
            {
                ShuttleTransferEndpoint candidate = endpoints[index];
                if (candidate == null || candidate == origin ||
                    candidate.DepotStock != order.Requester ||
                    candidate.StagingInventory != order.Requester.Inventory ||
                    candidate.TransferAnchor == null)
                    continue;
                if (directConsumer == null ||
                    string.CompareOrdinal(candidate.StableId, directConsumer.StableId) < 0)
                    directConsumer = candidate;
            }
            if (directConsumer != null)
                return directConsumer;

            ShuttleTransferEndpoint best = null;
            float bestDistance = float.PositiveInfinity;
            string bestKey = string.Empty;
            for (int index = 0; index < endpoints.Count; index++)
            {
                ShuttleTransferEndpoint candidate = endpoints[index];
                LogisticsStockComponent stock = candidate != null ? candidate.DepotStock : null;
                if (candidate == null || candidate == origin || stock == null ||
                    stock == source || stock == order.Requester ||
                    stock == origin.DepotStock || stock.Inventory == null ||
                    candidate.StagingInventory != stock.Inventory ||
                    candidate.TransferAnchor == null ||
                    !stock.TryGetPolicy(resource, out LogisticsStockPolicyEntry policy) ||
                    policy.role != LogisticsStockRole.Depot ||
                    !TryEstimateWalkingLeg(candidate.TransferAnchor,
                        order.Requester.FreightAnchor, out PersonnelRouteEstimate estimate))
                    continue;

                string stableKey = candidate.StableId ?? string.Empty;
                if (estimate.Distance < bestDistance - QuantityEpsilon ||
                    Mathf.Abs(estimate.Distance - bestDistance) <= QuantityEpsilon &&
                    string.CompareOrdinal(stableKey, bestKey) < 0)
                {
                    best = candidate;
                    bestDistance = estimate.Distance;
                    bestKey = stableKey;
                }
            }
            return best;
        }

        private Candidate FindShuttleLeadingCandidate(
            FreightOrder order,
            LogisticsStockComponent source,
            float desiredQuantity)
        {
            ShuttleManager shuttleManager = ShuttleManager.Instance;
            if (shuttleManager == null || source == null || source.Inventory == null)
                return null;

            float shuttleCapacity = GetMaximumShuttleCargoCapacity(shuttleManager);
            float requesterSpace = order.Requester.Inventory.FreeCapacity -
                                   GetProjectedIncomingCapacity(order.Requester.Inventory, null);
            float baseQuantity = FreightShipmentSizing.LimitToCapacity(
                desiredQuantity, source.Inventory.GetAvailable(order.Resource), requesterSpace,
                shuttleCapacity, order.Resource.IsDiscrete);
            if (baseQuantity <= QuantityEpsilon)
                return null;

            Candidate best = null;
            IReadOnlyList<ShuttleTransferEndpoint> endpoints = shuttleManager.Endpoints;
            for (int originIndex = 0; originIndex < endpoints.Count; originIndex++)
            {
                ShuttleTransferEndpoint origin = endpoints[originIndex];
                if (origin == null || origin.DepotStock != source ||
                    origin.StagingInventory != source.Inventory || origin.TransferAnchor == null ||
                    !source.TryGetPolicy(order.Resource, out LogisticsStockPolicyEntry sourcePolicy) ||
                    sourcePolicy.role == LogisticsStockRole.Consumer)
                    continue;

                ShuttleTransferEndpoint nearestDestination = FindNearestDestinationEndpoint(
                    order, source, origin, order.Resource, endpoints);
                if (nearestDestination == null)
                    continue;

                for (int destinationIndex = 0; destinationIndex < endpoints.Count; destinationIndex++)
                {
                    ShuttleTransferEndpoint destination = endpoints[destinationIndex];
                    if (destination != nearestDestination)
                        continue;
                    LogisticsStockComponent destinationStock = destination != null
                        ? destination.DepotStock : null;
                    if (destination == null || destination == origin || destinationStock == null ||
                        destinationStock.Inventory == null ||
                        destination.StagingInventory != destinationStock.Inventory ||
                        destination.TransferAnchor == null ||
                        !shuttleManager.CanService(origin, destination, ShuttlePayloadType.Freight))
                        continue;

                    float quantity = baseQuantity;
                    float walkingCost = 0f;
                    LogisticsRoutePlan route;
                    float flightDistance = Vector3.Distance(origin.TransferAnchor.position,
                        destination.TransferAnchor.position);
                    LogisticsRouteLeg shuttleLeg;
                    if (destinationStock == order.Requester)
                    {
                        if (destination.StagingInventory != order.Requester.Inventory)
                            continue;
                        shuttleLeg = LogisticsRouteLeg.Shuttle(source, order.Requester,
                            origin, destination, flightDistance);
                        route = new LogisticsRoutePlan(order, new[] { shuttleLeg });
                    }
                    else
                    {
                        if (!destinationStock.TryGetPolicy(order.Resource,
                                out LogisticsStockPolicyEntry destinationPolicy) ||
                            destinationPolicy.role != LogisticsStockRole.Depot)
                            continue;
                        float depotSpace = destinationStock.Inventory.FreeCapacity -
                            GetProjectedIncomingCapacity(destinationStock.Inventory, null);
                        quantity = FreightShipmentSizing.LimitToCapacity(
                            desiredQuantity, quantity, depotSpace,
                            shuttleCapacity, order.Resource.IsDiscrete);
                        if (quantity <= QuantityEpsilon ||
                            !TryEstimateWalkingLeg(destination.TransferAnchor,
                                order.Requester.FreightAnchor, out PersonnelRouteEstimate finalEstimate))
                            continue;

                        walkingCost = finalEstimate.Distance;
                        shuttleLeg = LogisticsRouteLeg.Shuttle(source, destinationStock,
                            origin, destination, flightDistance);
                        route = new LogisticsRoutePlan(order, new[]
                        {
                            shuttleLeg,
                            new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier,
                                destinationStock, order.Requester, finalEstimate.Distance)
                        });
                    }

                    if (quantity <= QuantityEpsilon)
                        continue;

                    if (!shuttleManager.TryQuoteFreightLeg(
                            shuttleLeg, quantity, out ShuttleFreightQuote shuttleQuote))
                        continue;

                    string key = "shuttle/" + origin.StableId + "/" + destination.StableId + "/" + order.Id;
                    Candidate candidate = new Candidate(shuttleQuote, source, quantity, key, route,
                        route.TotalEstimatedLoadedDistance + walkingCost);
                    if (best == null || Candidate.Compare(candidate, best) < 0)
                        best = candidate;
                }
            }
            return best;
        }

        private static bool TryEstimateWalkingLeg(
            Transform start,
            Transform destination,
            out PersonnelRouteEstimate estimate) =>
            TryEstimateWalkingLeg(start, destination, out estimate, out _);

        private static bool TryEstimateWalkingLeg(
            Transform start,
            Transform destination,
            out PersonnelRouteEstimate estimate,
            out string reason)
        {
            estimate = default;
            PersonnelRoutingManager routing = PersonnelRoutingManager.Instance;
            if (routing == null)
            {
                reason = PersonnelRoutingManager.MissingRoutingManagerReason;
                return false;
            }
            return routing.TryEstimateSharedWalkingPath(start, destination, out estimate, out reason);
        }

        private void LogWalkingLegUnavailable(FreightOrder order,
            LogisticsStockComponent source, LogisticsStockComponent destination, string reason)
        {
            string key = order.Id + ":" + source.GetEntityId() + ":" +
                destination.GetEntityId() + ":" + ModuleNavigationTopology.Revision;
            if (!walkingLegFailuresLogged.Add(key))
                return;
            if (walkingLegFailuresLogged.Count > 512)
                walkingLegFailuresLogged.Clear();
            Log("logistics.walking_leg_unavailable", "Info", source, destination,
                new SimulationLogField("demandId", order.Id),
                new SimulationLogField("resource", order.Resource.name),
                new SimulationLogField("topologyRevision", ModuleNavigationTopology.Revision),
                new SimulationLogField("reason", reason ?? string.Empty));
        }

        private static FreightWorkQuote FindBestServiceQuote(
            IReadOnlyList<WalkingFreightWorkService> services,
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            PersonnelRouteEstimate loadedEstimate,
            float quantity,
            FreightWorkPurpose purpose)
        {
            FreightWorkQuote best = null;
            for (int index = 0; index < services.Count; index++)
            {
                WalkingFreightWorkService service = services[index];
                if (service == null || !service.isActiveAndEnabled ||
                    !service.TryQuote(source, destination, finalDestination,
                        resource, quantity, purpose, loadedEstimate,
                        out FreightWorkQuote quote))
                    continue;
                if (best == null || quote.TotalServiceCost < best.TotalServiceCost ||
                    Mathf.Approximately(quote.TotalServiceCost, best.TotalServiceCost) &&
                    string.CompareOrdinal(quote.StableComparisonKey, best.StableComparisonKey) < 0)
                    best = quote;
            }
            return best;
        }

        private static float GetMaximumShuttleCargoCapacity(ShuttleManager shuttleManager)
        {
            float maximum = 0f;
            IReadOnlyList<ShuttleServiceComponent> shuttles = shuttleManager.Shuttles;
            for (int index = 0; index < shuttles.Count; index++)
            {
                ShuttleServiceComponent shuttle = shuttles[index];
                if (shuttle == null || !shuttle.isActiveAndEnabled ||
                    !shuttle.CanCarry(ShuttlePayloadType.Freight) || shuttle.CargoInventory == null)
                    continue;
                maximum = Mathf.Max(maximum, shuttle.CargoInventory.Capacity);
            }
            // Route discovery may proceed while the fleet is temporarily absent.
            // The shuttle leg will remain queued until a capable vehicle is registered.
            return maximum > QuantityEpsilon ? maximum : float.MaxValue;
        }

        private bool AcceptCandidate(FreightOrder order, Candidate candidate)
        {
            if (candidate == null || candidate.Provider == null || candidate.LegQuote == null ||
                candidate.Source == null || candidate.Source.Inventory == null ||
                candidate.RoutePlan == null || candidate.RoutePlan.Legs[0].Origin != candidate.Source)
                return false;

            if (!candidate.Source.Inventory.TryReserveOwned(
                    order.Resource, candidate.Quantity, out InventoryReservationToken reservation))
            {
                return false;
            }

            string executionId = "freight-execution-" +
                (++nextExecutionId).ToString("D6", CultureInfo.InvariantCulture);
            string allocationId = "freight-allocation-" +
                (++nextAllocationId).ToString("D6", CultureInfo.InvariantCulture);
            Transform sourceAnchor = candidate.Source.FreightAnchor;
            Transform destinationAnchor = order.Requester.FreightAnchor;
            FreightAllocation allocation = new FreightAllocation(
                allocationId, order, candidate.Source, candidate.Quantity, candidate.RoutePlan);
            FreightDeliveryJob job = new FreightDeliveryJob(
                allocation, null, reservation, jobMutationAuthority);
            if (!order.TryCommit(candidate.Quantity))
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                return false;
            }
            jobs.Add(job);
            ValidateDemandAccounting(order);
            if (!candidate.Provider.TryAcceptQuote(candidate.LegQuote, job, executionId,
                    out IFreightLegExecution execution) || execution == null)
            {
                candidate.Source.Inventory.ReleaseOwned(reservation);
                ReleaseOutstandingCommitment(job);
                job.SetState(jobMutationAuthority, FreightJobState.Cancelled);
                return false;
            }
            job.SetActiveLegExecution(jobMutationAuthority, execution);

            IFreightLegQuote acceptedQuote = candidate.LegQuote;
            UnityEngine.Object providerContext = execution.ProviderContext;
            float tripCapacity = acceptedQuote != null ? acceptedQuote.TripCapacity : 0f;

            Log("logistics.job_assigned", "Info", providerContext, order.Requester,
                new SimulationLogField("allocationId", job.Allocation.Id),
                new SimulationLogField("legIndex", job.CurrentLegIndex),
                new SimulationLogField("executionId", execution.ExecutionId),
                new SimulationLogField("demandId", order.Id),
                new SimulationLogField("resource", order.Resource.name),
                new SimulationLogField("quantity", candidate.Quantity),
                new SimulationLogField("source", candidate.Source.name),
                new SimulationLogField("distance", candidate.Distance),
                new SimulationLogField("legType", acceptedQuote != null
                    ? acceptedQuote.LegType.ToString() : string.Empty),
                new SimulationLogField("serviceCost", acceptedQuote != null
                    ? acceptedQuote.TotalServiceCost : 0f),
                new SimulationLogField("pickupAnchor", sourceAnchor != null ? sourceAnchor.name : string.Empty),
                new SimulationLogField("dropoffAnchor", destinationAnchor != null
                    ? destinationAnchor.name : string.Empty),
                 new SimulationLogField("routeLegs", job.RoutePlan.Legs.Count),
                new SimulationLogField("purpose", execution.IsEmergency
                    ? FreightWorkPurpose.ConsumerEmergencyPickup.ToString()
                    : acceptedQuote != null ? acceptedQuote.LegType.ToString() : "Unspecified"),
                new SimulationLogField("tripCapacity", tripCapacity),
                new SimulationLogField("estimatedTripCount", tripCapacity > 0f
                    ? Mathf.CeilToInt(candidate.Quantity / tripCapacity) : 0));
            return true;
        }

        private void ExpireUnpublishedOrders(long tick)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                FreightOrder order = orders[i];
                if (!order.IsOpen || order.Committed > QuantityEpsilon ||
                    tick - order.LastRefreshTick <= PublicationGraceTicks)
                    continue;

                RetireDemand(order);
                for (int j = jobs.Count - 1; j >= 0; j--)
                {
                    FreightDeliveryJob job = jobs[j];
                    if (job.Order == order && job.CurrentLegIndex == 0 && job.LegProgress == null &&
                        !job.HasPickedUp &&
                        !job.IsTerminal)
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
                else if (order.IsOpen && order.IsPolicyRetired &&
                         order.Committed <= QuantityEpsilon && order.Uncovered <= QuantityEpsilon)
                {
                    order.IsOpen = false;
                    Log("logistics.demand_target_satisfied", "Info", order.Requester, null,
                        new SimulationLogField("demandId", order.Id),
                        new SimulationLogField("resource", order.Resource.name),
                        new SimulationLogField("delivered", order.Delivered),
                        new SimulationLogField("retired", order.Retired));
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
            return order != null ? order.Uncovered : 0f;
        }

        public float GetAssuredFinalIncoming(FreightOrder order)
        {
            if (order == null)
                return 0f;

            float incoming = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job.Order == order && IsAssuredFinalExecution(job))
                    incoming += job.Quantity;
            }
            return incoming;
        }

        public float GetAssuredFinalIncoming(
            LogisticsStockComponent destination,
            ResourceDefinition resource)
        {
            if (destination == null || resource == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job.Destination == destination && job.Resource == resource &&
                    IsAssuredFinalExecution(job))
                    total += job.Quantity;
            }
            return total;
        }

        private bool IsAssuredFinalExecution(FreightDeliveryJob job)
        {
            return job != null && !job.IsTerminal && job.State != FreightJobState.Blocked &&
                   job.CurrentLegIndex == job.RoutePlan.Legs.Count - 1 &&
                   job.CurrentLeg != null && job.ActiveLegExecution != null &&
                   job.ActiveLegExecution.IsActive;
        }

        private void RecordFinalDelivery(FreightDeliveryJob job, float amount)
        {
            if (job == null || job.Order == null || amount <= QuantityEpsilon ||
                amount > job.Quantity + QuantityEpsilon)
                throw new InvalidOperationException("A final freight delivery must fit its live child shipment.");

            float accepted = Mathf.Min(amount, job.Quantity);
            if (!job.Order.RecordDelivery(accepted))
                throw new InvalidOperationException("Final delivery exceeds the live parent commitment.");
            job.AddDeliveredQuantity(jobMutationAuthority, accepted);
            job.SetQuantity(jobMutationAuthority, job.Quantity - accepted);
            ValidateDemandAccounting(job.Order);
        }

        private void ReleaseOutstandingCommitment(FreightDeliveryJob job)
        {
            if (job == null || job.Order == null || job.Quantity <= QuantityEpsilon)
                return;

            float released = job.Quantity;
            if (!job.Order.ReleaseCommitment(released))
                throw new InvalidOperationException("Released freight exceeds the live parent commitment.");
            if (job.Order.IsRetiring)
                job.Order.RetireUncovered();
            job.SetQuantity(jobMutationAuthority, 0f);
            ValidateDemandAccounting(job.Order);
        }

        private void ValidateDemandAccounting(FreightOrder order)
        {
            if (order == null)
                return;

            float childCommitment = 0f;
            for (int index = 0; index < jobs.Count; index++)
            {
                FreightDeliveryJob child = jobs[index];
                if (child != null && child.Order == order && !child.IsTerminal)
                    childCommitment += child.Quantity;
            }

            float unaccounted = order.Requested - order.Delivered - order.Committed - order.Retired;
            if (order.Committed < -QuantityEpsilon || order.Retired < -QuantityEpsilon ||
                unaccounted < -QuantityEpsilon ||
                Mathf.Abs(childCommitment - order.Committed) > QuantityEpsilon)
            {
                Log("logistics.demand_accounting_mismatch", "Error", order.Requester, null,
                    new SimulationLogField("demandId", order.Id),
                    new SimulationLogField("requested", order.Requested),
                    new SimulationLogField("delivered", order.Delivered),
                    new SimulationLogField("committed", order.Committed),
                    new SimulationLogField("retired", order.Retired),
                    new SimulationLogField("liveChildCommitment", childCommitment),
                    new SimulationLogField("uncovered", Mathf.Max(0f, unaccounted)));
            }
        }

        private float GetProjectedIncomingCapacity(
            InventoryComponent destination,
            FreightDeliveryJob except)
        {
            if (destination == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < jobs.Count; i++)
            {
                FreightDeliveryJob job = jobs[i];
                if (job == null || job == except || job.IsTerminal || job.Quantity <= QuantityEpsilon)
                    continue;

                for (int legIndex = job.CurrentLegIndex; legIndex < job.RoutePlan.Legs.Count; legIndex++)
                {
                    LogisticsRouteLeg leg = job.RoutePlan.Legs[legIndex];
                    if (leg.Destination == null || leg.Destination.Inventory != destination)
                        continue;

                    if (job.LegProgress != null && job.LegProgress.LegIndex == legIndex)
                    {
                        total += job.LegProgress.RemainingAtOrigin +
                                 job.LegProgress.InCarrierQuantity;
                    }
                    else if (job.Reservation == null || job.Reservation.Owner != destination)
                    {
                        total += job.Quantity;
                    }
                    break;
                }
            }
            return total;
        }

        private static bool HasPartialLegProgress(FreightDeliveryJob job)
        {
            FreightLegProgress progress = job != null ? job.LegProgress : null;
            return progress != null && (progress.ArrivedAtDestination > QuantityEpsilon ||
                                        progress.InCarrierQuantity > QuantityEpsilon);
        }

        private bool ShouldUseConsumerEmergencyPickup(FreightDeliveryJob job)
        {
            if (job == null || job.Order == null || job.CurrentLeg == null ||
                job.CurrentLegIndex <= 0 || job.CurrentLegIndex != job.RoutePlan.Legs.Count - 1 ||
                job.CurrentLeg.Type != LogisticsRouteLegType.WalkingCarrier ||
                job.Destination == null || job.Destination.Inventory == null ||
                !job.Destination.TryGetPolicy(job.Resource, out LogisticsStockPolicyEntry policy) ||
                policy.role != LogisticsStockRole.Consumer)
                return false;

            return ShouldUseConsumerEmergencyPickup(job.Order);
        }

        private bool ShouldUseConsumerEmergencyPickup(FreightOrder order)
        {
            if (order == null || order.Requester == null || order.Requester.Inventory == null ||
                !order.Requester.TryGetPolicy(order.Resource,
                    out LogisticsStockPolicyEntry policy) ||
                policy.role != LogisticsStockRole.Consumer)
                return false;

            float onHand = order.Requester.Inventory.GetOnHand(order.Resource);
            float assured = GetAssuredFinalIncoming(order);
            return onHand <= policy.emergencyThreshold + QuantityEpsilon &&
                   onHand + assured + QuantityEpsilon < policy.reorderThreshold;
        }

        private FreightOrder FindOpenOrder(LogisticsStockComponent requester, ResourceDefinition resource)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                FreightOrder order = orders[i];
                if (order != null && !order.IsRetiring &&
                    (order.IsOpen || order.Committed > QuantityEpsilon) &&
                    order.Requester == requester && order.Resource == resource)
                    return order;
            }
            return null;
        }

        private FreightOrder FindRetiringOrderWithCommitment(
            LogisticsStockComponent requester,
            ResourceDefinition resource)
        {
            for (int index = orders.Count - 1; index >= 0; index--)
            {
                FreightOrder order = orders[index];
                if (order != null && order.IsRetiring && order.Committed > QuantityEpsilon &&
                    order.Requester == requester && order.Resource == resource)
                    return order;
            }
            return null;
        }

        private void FinishExecution(FreightDeliveryJob job)
        {
            if (job != null && job.ActiveLegExecution != null)
                job.ActiveLegExecution.Complete(job);
        }

        private static void PruneTerminalHistory<T>(List<T> items, Predicate<T> isTerminal)
        {
            int terminalCount = 0;
            for (int index = 0; index < items.Count; index++)
                if (isTerminal(items[index]))
                    terminalCount++;
            int excess = terminalCount - TerminalHistoryLimit;
            if (excess <= 0)
                return;
            for (int index = 0; index < items.Count && excess > 0;)
            {
                if (isTerminal(items[index]))
                {
                    items.RemoveAt(index);
                    excess--;
                }
                else
                {
                    index++;
                }
            }
        }

        private IReadOnlyList<FreightOrder> SelectOrders(bool activeOnly)
        {
            List<FreightOrder> selected = new List<FreightOrder>();
            for (int index = 0; index < orders.Count; index++)
            {
                FreightOrder order = orders[index];
                bool activeOrder = order != null && (order.IsOpen || order.Committed > QuantityEpsilon);
                if (order != null && activeOrder == activeOnly)
                    selected.Add(order);
            }
            return selected.ToArray();
        }

        private IReadOnlyList<FreightDeliveryJob> SelectJobs(bool activeOnly)
        {
            List<FreightDeliveryJob> selected = new List<FreightDeliveryJob>();
            for (int index = 0; index < jobs.Count; index++)
            {
                FreightDeliveryJob job = jobs[index];
                bool activeJob = job != null && !job.IsTerminal;
                if (job != null && activeJob == activeOnly)
                    selected.Add(job);
            }
            return selected.ToArray();
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
                float quantity, string stableKey, LogisticsRoutePlan routePlan = null,
                float? serviceCost = null)
            {
                Provider = service;
                LegQuote = quote;
                Source = source;
                Quantity = quantity;
                Distance = serviceCost ?? (quote != null ? quote.TotalServiceCost : 0f);
                StableKey = stableKey;
                RoutePlan = routePlan;
            }

            public Candidate(ShuttleFreightQuote quote, LogisticsStockComponent source, float quantity,
                string stableKey, LogisticsRoutePlan routePlan, float serviceCost)
            {
                Provider = quote != null ? quote.Provider : null;
                LegQuote = quote;
                Source = source;
                Quantity = quantity;
                Distance = serviceCost;
                StableKey = stableKey;
                RoutePlan = routePlan;
            }

            public IFreightLegProvider Provider { get; }
            public IFreightLegQuote LegQuote { get; }
            public LogisticsStockComponent Source { get; }
            public float Quantity { get; }
            public float Distance { get; }
            public string StableKey { get; }
            public LogisticsRoutePlan RoutePlan { get; }

            public static int Compare(Candidate left, Candidate right)
            {
                return FreightCandidateRanking.Compare(
                    left.Quantity, left.Distance, left.StableKey,
                    right.Quantity, right.Distance, right.StableKey);
            }
        }
    }
}
