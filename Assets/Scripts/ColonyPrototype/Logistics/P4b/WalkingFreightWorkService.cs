using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Workplace-owned policy and provider for routine and emergency walking freight.
    /// Worker discovery and route estimates stay inside this service.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Walking Freight Work Service")]
    public sealed class WalkingFreightWorkService : MonoBehaviour,
        IWorkExecutionOwner, ISimulationTickable, ISimulationTickPriority,
        IFreightLegProvider
    {
        private static readonly List<WalkingFreightWorkService> active =
            new List<WalkingFreightWorkService>();

        [SerializeField] private WorkplaceComponent workplace;
        [SerializeField] private bool routineFreightEnabled;
        [SerializeField] private JobRoleDefinition routineRole;
        [SerializeField, Min(0.01f)] private float routineCapacityPerWorker = 10f;
        [SerializeField] private bool producerOutboundAssistEnabled;
        [SerializeField, Min(0.01f)] private float producerAssistCapacityPerWorker = 5f;
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
        public bool ProducerOutboundAssistEnabled => producerOutboundAssistEnabled;
        public float ProducerAssistCapacityPerWorker => producerAssistCapacityPerWorker;
        public bool EmergencyFreightEnabled => emergencyFreightEnabled;
        public float EmergencyCapacityPerWorker => emergencyCapacityPerWorker;
        public int MinimumWorkersRemainingAfterEmergencyDispatch =>
            minimumWorkersRemainingAfterEmergencyDispatch;
        public int ActiveExecutionCount => acceptedExecutions.Count;
        public int SimulationTickPriority => SimulationTickPriorities.LogisticsWorkExecution;

        private void Reset() => ResolveWorkplace();

        private void Awake() => ResolveWorkplace();

        private void OnEnable()
        {
            ResolveWorkplace();
            if (!active.Contains(this))
                active.Add(this);
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            for (int index = acceptedExecutions.Count - 1; index >= 0; index--)
            {
                WalkingFreightExecution execution = acceptedExecutions[index];
                if (execution == null || !execution.IsActive)
                {
                    acceptedExecutions.RemoveAt(index);
                    continue;
                }

                execution.SimulationTick();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => active.Clear();

        private void OnValidate()
        {
            ResolveWorkplace();
            routineCapacityPerWorker = Mathf.Max(0.01f, routineCapacityPerWorker);
            producerAssistCapacityPerWorker = Mathf.Max(0.01f, producerAssistCapacityPerWorker);
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

        public void ConfigureProducerOutboundAssist(bool enabled, float capacityPerWorker)
        {
            ResolveWorkplace();
            producerOutboundAssistEnabled = enabled;
            producerAssistCapacityPerWorker = Mathf.Max(0.01f, capacityPerWorker);
        }

        public bool TryQuote(
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            float desiredQuantity,
            FreightWorkPurpose purpose,
            out FreightWorkQuote quote)
        {
            quote = null;
            PersonnelRoutingManager routing = PersonnelRoutingManager.Instance;
            if (routing == null || source == null || destination == null ||
                source.FreightAnchor == null || destination.FreightAnchor == null ||
                !routing.TryEstimateSharedWalkingPath(source.FreightAnchor,
                    destination.FreightAnchor, out PersonnelRouteEstimate loaded, out _))
                return false;

            return TryQuote(source, destination, finalDestination, resource,
                desiredQuantity, purpose, loaded, out quote);
        }

        public bool TryQuote(
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            float desiredQuantity,
            FreightWorkPurpose purpose,
            PersonnelRouteEstimate loaded,
            out FreightWorkQuote quote)
        {
            quote = null;
            ResolveWorkplace();
            if (!isActiveAndEnabled || workplace == null || source == null || destination == null ||
                source == destination || resource == null || !IsFinitePositive(desiredQuantity) ||
                !IsPolicyAvailableForLeg(source, destination, finalDestination,
                    resource, purpose) ||
                WorkforceManager.Instance == null ||
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
                    (purpose == FreightWorkPurpose.RoutinePorter && assignment.Role != routineRole))
                {
                    continue;
                }

                ColonistIdentity worker = assignment.Colonist;
                if (!IsWorkerAvailable(worker, assignment.Role, gameHour, purpose))
                    continue;
                if (!TryEstimatePositioning(worker, source,
                        out PersonnelRouteEstimate positioning))
                {
                    continue;
                }

                InventoryComponent inventory = worker.GetComponent<InventoryComponent>();
                float tripCapacity = Mathf.Min(GetTripCapacity(purpose), inventory.FreeCapacity);
                if (tripCapacity <= 0.0001f)
                    continue;

                FreightWorkQuote candidate = new FreightWorkQuote(
                    this,
                    worker,
                    source,
                    destination,
                    finalDestination,
                    resource,
                    desiredQuantity,
                    positioning.Distance,
                    loaded.Distance,
                    tripCapacity,
                    purpose);
                if (best == null || CompareWorkerQuotes(candidate, best) < 0)
                    best = candidate;
            }

            quote = best;
            return quote != null;
        }

        public bool TryAcceptQuote(
            FreightWorkQuote quote,
            float quantity,
            string executionId,
            out WalkingFreightExecution execution)
        {
            execution = null;
            ResolveWorkplace();
            if (quote == null || quote.Provider != this || string.IsNullOrWhiteSpace(executionId) ||
                !isActiveAndEnabled ||
                !IsFinitePositive(quantity) || quantity > quote.ShipmentQuantity + 0.0001f ||
                quote.SelectedWorker == null || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null ||
                !IsPolicyAvailableForLeg(quote.Source, quote.Destination,
                    quote.FinalDestination, quote.Resource, quote.Purpose))
            {
                return false;
            }

            ColonistIdentity worker = quote.SelectedWorker;
            float gameHour = SimulationManager.Instance.CurrentGameHour;
            if (!WorkforceManager.Instance.TryGetAssignment(worker, out WorkAssignment assignment) ||
                assignment == null || assignment.Workplace != workplace ||
                (quote.Purpose == FreightWorkPurpose.RoutinePorter && assignment.Role != routineRole) ||
                !IsWorkerAvailable(worker, assignment.Role, gameHour, quote.Purpose) ||
                !TryEstimatePositioning(worker, quote.Source,
                    out PersonnelRouteEstimate positioning) ||
                !PersonnelRoutingManager.Instance.TryEstimateSharedWalkingPath(
                    quote.Source.FreightAnchor, quote.Destination.FreightAnchor,
                    out PersonnelRouteEstimate loaded, out _))
            {
                return false;
            }

            InventoryComponent inventory = worker.GetComponent<InventoryComponent>();
            float tripCapacity = Mathf.Min(GetTripCapacity(quote.Purpose), inventory.FreeCapacity);
            if (tripCapacity <= 0.0001f ||
                Mathf.Min(tripCapacity, quantity) > quote.TripCapacity + 0.0001f)
                return false;

            ColonistBrain brain = worker.GetComponent<ColonistBrain>();
            if (brain == null || !brain.TryAcquireWorkExecution(workplace, this,
                    out WorkExecutionLease lease))
            {
                return false;
            }

            ColonistActivityRunner activityRunner = worker.GetComponent<ColonistActivityRunner>();
            bool suspendsFacilityActivity = workplace.ExecutionMode == WorkplaceExecutionMode.FacilityActivity;
            if (suspendsFacilityActivity)
            {
                if (activityRunner == null || !activityRunner.IsActivityActive)
                {
                    lease.Release();
                    return false;
                }
                activityRunner.Stop();
            }

            execution = new WalkingFreightExecution(
                this,
                worker,
                executionId,
                lease,
                assignment.Role,
                tripCapacity,
                positioning.Distance,
                loaded.Distance,
                quote.Purpose);
            acceptedExecutions.Add(execution);
            SimulationLogManager.RecordEvent(
                "logistics.service_execution_assigned",
                "Logistics",
                "Info",
                worker,
                this,
                new SimulationLogField("workplace", workplace.name),
                new SimulationLogField("role", assignment.Role.StableId),
                new SimulationLogField("purpose", quote.Purpose.ToString()),
                new SimulationLogField("tripCapacity", tripCapacity),
                new SimulationLogField("shipmentQuantity", quantity),
                new SimulationLogField("estimatedTripCount",
                    Mathf.CeilToInt(quantity / Mathf.Max(0.0001f, tripCapacity))),
                new SimulationLogField("positioningDistance", positioning.Distance),
                new SimulationLogField("loadedCargoDistance", loaded.Distance));
            return true;
        }

        bool IFreightLegProvider.TryAcceptQuote(
            IFreightLegQuote quote,
            FreightDeliveryJob job,
            string executionId,
            out IFreightLegExecution execution)
        {
            execution = null;
            if (!(quote is FreightWorkQuote walkingQuote) || walkingQuote.Provider != this || job == null ||
                job.CurrentLeg == null || job.CurrentLeg.Type != LogisticsRouteLegType.WalkingCarrier ||
                job.CurrentLeg.Origin != walkingQuote.Source ||
                job.CurrentLeg.Destination != walkingQuote.Destination ||
                job.Destination != walkingQuote.FinalDestination ||
                job.Resource != walkingQuote.Resource ||
                !TryAcceptQuote(walkingQuote, job.Quantity, executionId,
                    out WalkingFreightExecution walkingExecution))
                return false;

            if (!walkingExecution.PrepareCargo(job.Resource) || !walkingExecution.Assign(job))
            {
                walkingExecution.CancelBeforeAssignment();
                return false;
            }

            execution = walkingExecution;
            return true;
        }

        internal bool TryEstimateWalkingPathFrom(
            Vector3 startPosition,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            estimate = default;
            if (destination == null || workplace == null || WorkforceManager.Instance == null ||
                PersonnelRoutingManager.Instance == null)
                return false;

            bool found = false;
            IReadOnlyList<WorkAssignment> assignments = WorkforceManager.Instance.Assignments;
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment == null || assignment.Workplace != workplace ||
                    assignment.Colonist == null)
                    continue;
                if (!PersonnelRoutingManager.Instance.TryEstimateWalkOnly(
                        assignment.Colonist, startPosition, destination,
                        out PersonnelRouteEstimate candidate, out _))
                    continue;
                if (!found || candidate.Distance < estimate.Distance)
                {
                    estimate = candidate;
                    found = true;
                }
            }
            return found;
        }

        internal void ReleaseExecution(WalkingFreightExecution execution)
        {
            acceptedExecutions.Remove(execution);
        }

        public WorkReleaseDisposition RequestRelease(
            WorkExecutionLease lease,
            WorkReleaseReason reason)
        {
            for (int index = 0; index < acceptedExecutions.Count; index++)
            {
                WalkingFreightExecution execution = acceptedExecutions[index];
                if (execution != null && execution.OwnsLease(lease))
                    return execution.RequestRelease(reason);
            }

            return WorkReleaseDisposition.ReleasedNow;
        }

        public string GetStableKey()
        {
            ResolveWorkplace();
            return SceneStableIdentity.GetKey(this);
        }

        internal bool IsPolicyAvailableForLeg(
            LogisticsStockComponent source,
            LogisticsStockComponent legDestination,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            FreightWorkPurpose purpose)
        {
            ResolveWorkplace();
            if (source == null || legDestination == null || source == legDestination)
                return false;
            return IsPolicyAvailable(source, finalDestination, resource, purpose);
        }

        private bool IsPolicyAvailable(
            LogisticsStockComponent source,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            FreightWorkPurpose purpose)
        {
            if (workplace == null)
                return false;

            if (purpose == FreightWorkPurpose.ConsumerEmergencyPickup)
            {
                return emergencyFreightEnabled &&
                       workplace.ExecutionMode == WorkplaceExecutionMode.FacilityActivity &&
                       finalDestination != null &&
                       finalDestination.GetComponentInParent<WorkplaceComponent>() == workplace &&
                       finalDestination.TryGetPolicy(resource,
                           out LogisticsStockPolicyEntry consumerPolicy) &&
                       consumerPolicy.role == LogisticsStockRole.Consumer;
            }

            if (purpose == FreightWorkPurpose.ProducerOutboundAssist)
            {
                return producerOutboundAssistEnabled && source != null &&
                       source.GetComponentInParent<WorkplaceComponent>() == workplace &&
                       source.TryGetPolicy(resource, out LogisticsStockPolicyEntry producerPolicy) &&
                       producerPolicy.role == LogisticsStockRole.Producer;
            }

            return purpose == FreightWorkPurpose.RoutinePorter && routineFreightEnabled &&
                   routineRole != null && workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty;
        }

        private bool IsWorkerAvailable(
            ColonistIdentity worker,
            JobRoleDefinition role,
            float gameHour,
            FreightWorkPurpose purpose)
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
            ColonistActivityRunner activityRunner = worker.GetComponent<ColonistActivityRunner>();
            if (brain == null || !brain.CanAcquireWorkExecution(workplace) ||
                inventory == null || routeRunner == null || routeRunner.IsExecuting ||
                workplace.ExecutionMode == WorkplaceExecutionMode.FacilityActivity &&
                    (activityRunner == null || !activityRunner.IsActivityActive) ||
                IsAlreadyAccepted(worker))
            {
                return false;
            }

            if (purpose != FreightWorkPurpose.ConsumerEmergencyPickup)
                return true;

            return CountActiveWorkplaceStaff(gameHour) - 1 >=
                   minimumWorkersRemainingAfterEmergencyDispatch;
        }

        private float GetTripCapacity(FreightWorkPurpose purpose)
        {
            switch (purpose)
            {
                case FreightWorkPurpose.RoutinePorter:
                    return routineCapacityPerWorker;
                case FreightWorkPurpose.ProducerOutboundAssist:
                    return producerAssistCapacityPerWorker;
                case FreightWorkPurpose.ConsumerEmergencyPickup:
                    return emergencyCapacityPerWorker;
                default:
                    return 0f;
            }
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

        private static bool TryEstimatePositioning(
            ColonistIdentity worker,
            LogisticsStockComponent source,
            out PersonnelRouteEstimate positioning)
        {
            positioning = default;
            PersonnelRoutingManager routing = PersonnelRoutingManager.Instance;
            if (routing == null || worker == null || source == null || source.FreightAnchor == null ||
                !routing.TryPlanRoute(worker, source.FreightAnchor,
                    out PersonnelRoutePlan positioningPlan, out _) ||
                positioningPlan == null)
                return false;

            positioning = new PersonnelRouteEstimate(worker.transform.position,
                source.FreightAnchor.position, positioningPlan.TotalEstimatedDistance);
            return true;
        }

        private static int CompareWorkerQuotes(FreightWorkQuote left, FreightWorkQuote right)
        {
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
}
