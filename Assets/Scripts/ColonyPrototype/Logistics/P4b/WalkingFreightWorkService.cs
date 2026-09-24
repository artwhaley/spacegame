using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>A provider's proposal for moving one cargo allocation by walking.</summary>
    public sealed class FreightWorkQuote
    {
        internal FreightWorkQuote(
            WalkingFreightWorkService provider,
            ColonistIdentity selectedWorker,
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            ResourceDefinition resource,
            float requestedQuantity,
            float maximumUsefulQuantity,
            float positioningCost,
            float loadedCargoTravelCost,
            float capacity,
            bool emergency)
        {
            Provider = provider;
            SelectedWorker = selectedWorker;
            Source = source;
            Destination = destination;
            Resource = resource;
            RequestedQuantity = requestedQuantity;
            MaximumUsefulQuantity = maximumUsefulQuantity;
            PositioningCost = positioningCost;
            LoadedCargoTravelCost = loadedCargoTravelCost;
            Capacity = capacity;
            IsEmergency = emergency;
            StableComparisonKey = provider.GetStableKey();
        }

        public WalkingFreightWorkService Provider { get; }
        public float MaximumUsefulQuantity { get; }
        public float PositioningCost { get; }
        public float LoadedCargoTravelCost { get; }
        public float TotalServiceCost => PositioningCost + LoadedCargoTravelCost;
        public string StableComparisonKey { get; }

        internal ColonistIdentity SelectedWorker { get; }
        internal LogisticsStockComponent Source { get; }
        internal LogisticsStockComponent Destination { get; }
        internal ResourceDefinition Resource { get; }
        internal float RequestedQuantity { get; }
        internal float Capacity { get; }
        internal bool IsEmergency { get; }
    }

    /// <summary>
    /// Workplace-owned policy and provider for routine and emergency walking freight.
    /// Worker discovery and route estimates stay inside this service.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Walking Freight Work Service")]
    public sealed class WalkingFreightWorkService : MonoBehaviour
    {
        private static readonly List<WalkingFreightWorkService> active =
            new List<WalkingFreightWorkService>();

        [SerializeField] private WorkplaceComponent workplace;
        [SerializeField] private bool routineFreightEnabled;
        [SerializeField] private JobRoleDefinition routineRole;
        [SerializeField, Min(0.01f)] private float routineCapacityPerWorker = 10f;
        [SerializeField] private bool emergencyFreightEnabled;
        [SerializeField, Min(0.01f)] private float emergencyCapacityPerWorker = 5f;
        [SerializeField, Min(0)] private int minimumWorkersRemainingAfterEmergencyDispatch;

        private readonly List<WalkingFreightExecution> acceptedExecutions =
            new List<WalkingFreightExecution>();

        public static IReadOnlyList<WalkingFreightWorkService> Active => active;
        public WorkplaceComponent Workplace => workplace;
        public bool RoutineFreightEnabled => routineFreightEnabled;
        public JobRoleDefinition RoutineRole => routineRole;
        public float RoutineCapacityPerWorker => routineCapacityPerWorker;
        public bool EmergencyFreightEnabled => emergencyFreightEnabled;
        public float EmergencyCapacityPerWorker => emergencyCapacityPerWorker;
        public int MinimumWorkersRemainingAfterEmergencyDispatch =>
            minimumWorkersRemainingAfterEmergencyDispatch;
        public int ActiveExecutionCount => acceptedExecutions.Count;

        private void Reset() => ResolveWorkplace();

        private void Awake() => ResolveWorkplace();

        private void OnEnable()
        {
            ResolveWorkplace();
            if (!active.Contains(this))
                active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => active.Clear();

        private void OnValidate()
        {
            ResolveWorkplace();
            routineCapacityPerWorker = Mathf.Max(0.01f, routineCapacityPerWorker);
            emergencyCapacityPerWorker = Mathf.Max(0.01f, emergencyCapacityPerWorker);
            minimumWorkersRemainingAfterEmergencyDispatch =
                Mathf.Max(0, minimumWorkersRemainingAfterEmergencyDispatch);
        }

        public void ConfigureRoutineFreight(
            bool enabled,
            JobRoleDefinition role,
            float capacityPerWorker)
        {
            ResolveWorkplace();
            routineFreightEnabled = enabled;
            routineRole = role;
            routineCapacityPerWorker = Mathf.Max(0.01f, capacityPerWorker);
        }

        public void ConfigureEmergencyFreight(
            bool enabled,
            float capacityPerWorker,
            int minimumWorkersRemaining)
        {
            ResolveWorkplace();
            emergencyFreightEnabled = enabled;
            emergencyCapacityPerWorker = Mathf.Max(0.01f, capacityPerWorker);
            minimumWorkersRemainingAfterEmergencyDispatch = Mathf.Max(0, minimumWorkersRemaining);
        }

        public bool TryQuote(
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            ResourceDefinition resource,
            float desiredQuantity,
            bool emergency,
            out FreightWorkQuote quote)
        {
            quote = null;
            ResolveWorkplace();
            if (!isActiveAndEnabled || workplace == null || source == null || destination == null ||
                source == destination || resource == null || !IsFinitePositive(desiredQuantity) ||
                !IsPolicyAvailable(destination, emergency) || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null || PersonnelRoutingManager.Instance == null ||
                source.FreightAnchor == null || destination.FreightAnchor == null)
            {
                return false;
            }

            FreightWorkQuote best = null;
            IReadOnlyList<WorkAssignment> assignments = WorkforceManager.Instance.Assignments;
            float gameHour = SimulationManager.Instance.CurrentGameHour;
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment == null || assignment.Colonist == null ||
                    assignment.Workplace != workplace ||
                    (!emergency && assignment.Role != routineRole))
                {
                    continue;
                }

                ColonistIdentity worker = assignment.Colonist;
                if (!IsWorkerAvailable(worker, assignment.Role, gameHour, emergency))
                    continue;
                if (!TryEstimate(worker, source, destination,
                        out PersonnelRouteEstimate positioning,
                        out PersonnelRouteEstimate loaded))
                {
                    continue;
                }

                float capacity = emergency
                    ? emergencyCapacityPerWorker
                    : routineCapacityPerWorker;
                InventoryComponent inventory = worker.GetComponent<InventoryComponent>();
                float availableCapacity = Mathf.Max(0f,
                    capacity - inventory.GetOnHand(resource));
                float usefulQuantity = Mathf.Min(desiredQuantity, availableCapacity);
                if (usefulQuantity <= 0.0001f)
                    continue;

                FreightWorkQuote candidate = new FreightWorkQuote(
                    this,
                    worker,
                    source,
                    destination,
                    resource,
                    desiredQuantity,
                    usefulQuantity,
                    positioning.Distance,
                    loaded.Distance,
                    capacity,
                    emergency);
                if (best == null || CompareWorkerQuotes(candidate, best) < 0)
                    best = candidate;
            }

            quote = best;
            return quote != null;
        }

        public bool TryAcceptQuote(
            FreightWorkQuote quote,
            float quantity,
            out WalkingFreightExecution execution)
        {
            execution = null;
            ResolveWorkplace();
            if (quote == null || quote.Provider != this || !isActiveAndEnabled ||
                !IsFinitePositive(quantity) || quantity > quote.MaximumUsefulQuantity + 0.0001f ||
                quote.SelectedWorker == null || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null || !IsPolicyAvailable(quote.Destination, quote.IsEmergency))
            {
                return false;
            }

            ColonistIdentity worker = quote.SelectedWorker;
            float gameHour = SimulationManager.Instance.CurrentGameHour;
            if (!WorkforceManager.Instance.TryGetAssignment(worker, out WorkAssignment assignment) ||
                assignment == null || assignment.Workplace != workplace ||
                (!quote.IsEmergency && assignment.Role != routineRole) ||
                !IsWorkerAvailable(worker, assignment.Role, gameHour, quote.IsEmergency) ||
                !TryEstimate(worker, quote.Source, quote.Destination,
                    out PersonnelRouteEstimate positioning,
                    out PersonnelRouteEstimate loaded))
            {
                return false;
            }

            float capacity = quote.IsEmergency
                ? emergencyCapacityPerWorker
                : routineCapacityPerWorker;
            InventoryComponent inventory = worker.GetComponent<InventoryComponent>();
            float availableCapacity = Mathf.Max(0f, capacity - inventory.GetOnHand(quote.Resource));
            if (quantity > availableCapacity + 0.0001f)
                return false;

            WalkingFreightCarrierComponent legacyExecution =
                worker.GetComponent<WalkingFreightCarrierComponent>();
            if (legacyExecution == null)
                return false;

            if (quote.IsEmergency && !legacyExecution.BeginEmergencyExcursion(workplace))
                return false;

            execution = new WalkingFreightExecution(
                this,
                worker,
                legacyExecution,
                capacity,
                positioning.Distance,
                loaded.Distance,
                quote.IsEmergency);
            acceptedExecutions.Add(execution);
            return true;
        }

        internal void ReleaseExecution(WalkingFreightExecution execution)
        {
            acceptedExecutions.Remove(execution);
        }

        public string GetStableKey()
        {
            ResolveWorkplace();
            string scene = gameObject.scene.path;
            string hierarchy = string.Empty;
            for (Transform current = transform; current != null; current = current.parent)
                hierarchy = current.name + "[" + current.GetSiblingIndex() + "]/" + hierarchy;
            return (string.IsNullOrEmpty(scene) ? gameObject.scene.name : scene) + "/" + hierarchy;
        }

        private bool IsPolicyAvailable(LogisticsStockComponent destination, bool emergency)
        {
            if (workplace == null)
                return false;

            if (emergency)
            {
                return emergencyFreightEnabled &&
                       workplace.ExecutionMode == WorkplaceExecutionMode.FacilityActivity &&
                       destination != null &&
                       destination.GetComponent<WorkplaceComponent>() == workplace;
            }

            return routineFreightEnabled && routineRole != null &&
                   workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty;
        }

        private bool IsWorkerAvailable(
            ColonistIdentity worker,
            JobRoleDefinition role,
            float gameHour,
            bool emergency)
        {
            if (worker == null || role == null || !worker.isActiveAndEnabled ||
                WorkforceManager.Instance == null || workplace == null ||
                !WorkforceManager.Instance.IsGenuinelyOnDuty(worker, workplace, role, gameHour))
            {
                return false;
            }

            ColonistBrain brain = worker.GetComponent<ColonistBrain>();
            InventoryComponent inventory = worker.GetComponent<InventoryComponent>();
            PersonnelRouteRunner routeRunner = worker.GetComponent<PersonnelRouteRunner>();
            WalkingFreightCarrierComponent legacyExecution =
                worker.GetComponent<WalkingFreightCarrierComponent>();
            if (brain == null || brain.State != ColonistBrainState.Working ||
                brain.IsCriticallyHungry || brain.ActiveWorkExecutionLease != null ||
                inventory == null || routeRunner == null || routeRunner.IsExecuting ||
                legacyExecution == null || legacyExecution.HasActiveJob ||
                IsAlreadyAccepted(worker))
            {
                return false;
            }

            if (!emergency)
                return true;

            return CountActiveWorkplaceStaff(gameHour) - 1 >=
                   minimumWorkersRemainingAfterEmergencyDispatch;
        }

        private int CountActiveWorkplaceStaff(float gameHour)
        {
            if (WorkforceManager.Instance == null)
                return 0;

            int count = 0;
            IReadOnlyList<WorkAssignment> assignments = WorkforceManager.Instance.Assignments;
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment == null || assignment.Colonist == null ||
                    assignment.Workplace != workplace || assignment.Role == null)
                {
                    continue;
                }

                ColonistBrain brain = assignment.Colonist.GetComponent<ColonistBrain>();
                if (brain != null && !brain.IsCriticallyHungry &&
                    brain.ActiveWorkExecutionLease == null && !IsAlreadyAccepted(assignment.Colonist) &&
                    WorkforceManager.Instance.IsGenuinelyOnDuty(
                        assignment.Colonist, workplace, assignment.Role, gameHour))
                {
                    count++;
                }
            }
            return count;
        }

        private bool IsAlreadyAccepted(ColonistIdentity worker)
        {
            for (int index = 0; index < acceptedExecutions.Count; index++)
            {
                WalkingFreightExecution execution = acceptedExecutions[index];
                if (execution != null && execution.IsActive && execution.IsForWorker(worker))
                    return true;
            }
            return false;
        }

        private static bool TryEstimate(
            ColonistIdentity worker,
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            out PersonnelRouteEstimate positioning,
            out PersonnelRouteEstimate loaded)
        {
            positioning = default;
            loaded = default;
            PersonnelRoutingManager routing = PersonnelRoutingManager.Instance;
            return routing != null && worker != null && source != null && destination != null &&
                   source.FreightAnchor != null && destination.FreightAnchor != null &&
                   routing.TryEstimate(worker, source.FreightAnchor, out positioning) &&
                   routing.TryEstimateFrom(worker, source.FreightAnchor.position,
                       destination.FreightAnchor, out loaded);
        }

        private static int CompareWorkerQuotes(FreightWorkQuote left, FreightWorkQuote right)
        {
            int usefulQuantity = right.MaximumUsefulQuantity.CompareTo(left.MaximumUsefulQuantity);
            if (usefulQuantity != 0)
                return usefulQuantity;
            int serviceCost = left.TotalServiceCost.CompareTo(right.TotalServiceCost);
            if (serviceCost != 0)
                return serviceCost;

            string leftWorkerKey = PersonnelRouteIdentity.GetStableKey(left.SelectedWorker);
            string rightWorkerKey = PersonnelRouteIdentity.GetStableKey(right.SelectedWorker);
            return string.CompareOrdinal(leftWorkerKey, rightWorkerKey);
        }

        private void ResolveWorkplace()
        {
            if (workplace == null)
                workplace = GetComponent<WorkplaceComponent>();
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    /// <summary>One service-accepted walking job; worker identity stays inside the provider execution.</summary>
    public sealed class WalkingFreightExecution
    {
        private readonly ColonistIdentity worker;
        private readonly WalkingFreightCarrierComponent legacyExecution;

        internal WalkingFreightExecution(
            WalkingFreightWorkService service,
            ColonistIdentity worker,
            WalkingFreightCarrierComponent legacyExecution,
            float capacity,
            float positioningDistance,
            float loadedCargoTravelCost,
            bool emergency)
        {
            Service = service;
            this.worker = worker;
            this.legacyExecution = legacyExecution;
            MaximumCapacity = capacity;
            PositioningDistance = positioningDistance;
            LoadedCargoTravelCost = loadedCargoTravelCost;
            IsEmergency = emergency;
            IsActive = true;
        }

        public WalkingFreightWorkService Service { get; }
        public InventoryComponent CargoInventory => worker != null
            ? worker.GetComponent<InventoryComponent>()
            : null;
        public float MaximumCapacity { get; }
        public float PositioningDistance { get; }
        public float LoadedCargoTravelCost { get; }
        public bool IsEmergency { get; }
        public bool IsActive { get; private set; }

        internal bool IsForWorker(ColonistIdentity candidate) => worker == candidate;

        internal bool PrepareCargo(ResourceDefinition resource)
        {
            if (!IsActive || legacyExecution == null || CargoInventory == null || resource == null)
                return false;

            legacyExecution.ConfigureCapacity(MaximumCapacity);
            return CargoInventory.SetCapacity(resource, MaximumCapacity);
        }

        internal bool Assign(FreightDeliveryJob job) =>
            IsActive && legacyExecution != null && legacyExecution.Assign(job);

        internal void MarkCargoLoaded()
        {
            if (IsActive && IsEmergency && legacyExecution != null && legacyExecution.Brain != null)
                legacyExecution.Brain.SetWorkExcursionCargo(true);
        }

        internal void CancelBeforeAssignment()
        {
            if (!IsActive)
                return;

            if (IsEmergency && legacyExecution != null && legacyExecution.Brain != null)
                legacyExecution.Brain.CompleteWorkExcursion();
            Release();
        }

        internal void Complete(FreightDeliveryJob job)
        {
            if (!IsActive)
                return;

            if (legacyExecution != null)
            {
                legacyExecution.RouteRunner?.StopRoute();
                legacyExecution.Complete(job);
                if (IsEmergency && legacyExecution.Brain != null)
                    legacyExecution.Brain.CompleteWorkExcursion();
            }
            Release();
        }

        private void Release()
        {
            if (!IsActive)
                return;
            IsActive = false;
            Service?.ReleaseExecution(this);
        }
    }
}
