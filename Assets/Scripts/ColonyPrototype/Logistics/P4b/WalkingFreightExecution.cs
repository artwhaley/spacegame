using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>One service-accepted walking job; worker identity stays inside the provider execution.</summary>
    public sealed class WalkingFreightExecution : IFreightLegExecution
    {
        private readonly ColonistIdentity worker;
        private readonly PersonnelRouteRunner routeRunner;
        private readonly ColonistActivityRunner activityRunner;
        private readonly WorkExecutionLease lease;
        private readonly JobRoleDefinition assignedRole;
        private FreightDeliveryJob job;
        private bool returningToDuty;
        private long retryAtTick;
        private string activeRouteId = string.Empty;
        private string activeRouteStableId = string.Empty;
        private bool routeStartInProgress;
        private string pendingRouteCompletedId;
        private string pendingRouteFailedId;
        private string pendingRouteFailedReason;

        internal WalkingFreightExecution(
            WalkingFreightWorkService service,
            ColonistIdentity worker,
            string executionId,
            WorkExecutionLease lease,
            JobRoleDefinition assignedRole,
            float capacity,
            float positioningDistance,
            float loadedCargoTravelCost,
            bool emergency)
        {
            Service = service;
            ExecutionId = executionId;
            this.worker = worker;
            routeRunner = worker != null ? worker.GetComponent<PersonnelRouteRunner>() : null;
            activityRunner = worker != null ? worker.GetComponent<ColonistActivityRunner>() : null;
            this.lease = lease;
            this.assignedRole = assignedRole;
            MaximumCapacity = capacity;
            PositioningDistance = positioningDistance;
            LoadedCargoTravelCost = loadedCargoTravelCost;
            IsEmergency = emergency;
            IsActive = true;
            if (routeRunner != null)
            {
                routeRunner.ApproachRouteCompleted += HandleApproachRouteCompleted;
                routeRunner.ApproachRouteFailed += HandleApproachRouteFailed;
            }
        }

        public WalkingFreightWorkService Service { get; }
        public string ExecutionId { get; }
        public int LegIndex { get; private set; }
        public UnityEngine.Object ProviderContext => Service;
        public InventoryComponent CargoInventory => worker != null
            ? worker.GetComponent<InventoryComponent>()
            : null;
        public float MaximumCapacity { get; }
        public float PositioningDistance { get; }
        public float LoadedCargoTravelCost { get; }
        public bool IsEmergency { get; }
        public bool IsActive { get; private set; }

        internal bool IsForWorker(ColonistIdentity candidate) => worker == candidate;
        internal bool OwnsLease(WorkExecutionLease candidate) => lease == candidate;

        internal bool PrepareCargo(ResourceDefinition resource)
        {
            if (!IsActive || CargoInventory == null || resource == null)
                return false;

            return CargoInventory.SetCapacity(resource, MaximumCapacity);
        }

        internal bool Assign(FreightDeliveryJob assignedJob)
        {
            if (!IsActive || assignedJob == null || job != null)
                return false;

            job = assignedJob;
            LegIndex = assignedJob.CurrentLegIndex;
            return true;
        }

        internal void CancelBeforeAssignment()
        {
            if (!IsActive)
                return;

            if (IsEmergency && ShouldResumeFacilityWork())
                ResumeFacilityWork();
            Release();
        }

        void IFreightLegExecution.Complete(FreightDeliveryJob assignedJob) => Complete(assignedJob);

        internal void Complete(FreightDeliveryJob job)
        {
            if (!IsActive || this.job != job)
                return;

            if (IsEmergency)
            {
                if (ShouldResumeFacilityWork())
                    ResumeFacilityWork();
                Release();
                return;
            }

            if (ShouldReturnToDuty())
                StartReturnToDutyRoute();
            else
                Release();
        }

        internal void SimulationTick()
        {
            if (!IsActive)
                return;

            if (returningToDuty)
            {
                if (SimulationManager.Instance != null &&
                    SimulationManager.Instance.CurrentTick >= retryAtTick)
                    StartReturnToDutyRoute();
                return;
            }

            if (job == null || job.IsTerminal)
                return;

            if (job.State == FreightJobState.Assigned)
            {
                if (lease != null && lease.HasPendingReleaseRequest)
                {
                    FreightLogisticsManager.Instance?.RequestExecutionRelease(
                        job, this, CreateCorrelation(),
                        lease.PendingReleaseReason ?? WorkReleaseReason.OtherWorkPolicy);
                    return;
                }

                if (IsEmergency && activityRunner != null && activityRunner.HasActiveRequest)
                    return;

                StartPickupRoute();
                return;
            }

            if (job.State == FreightJobState.Blocked && SimulationManager.Instance != null &&
                SimulationManager.Instance.CurrentTick >= job.RetryAtTick)
            {
                StartDeliveryRoute();
            }
        }

        internal WorkReleaseDisposition RequestRelease(WorkReleaseReason reason)
        {
            if (!IsActive)
                return WorkReleaseDisposition.ReleasedNow;

            if (job == null || job.IsTerminal)
            {
                Release();
                return WorkReleaseDisposition.ReleasedNow;
            }

            FreightLogisticsManager manager = FreightLogisticsManager.Instance;
            return manager != null
                ? manager.RequestExecutionRelease(job, this, CreateCorrelation(), reason)
                : WorkReleaseDisposition.ReleasedNow;
        }

        private void StartPickupRoute()
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            Transform anchor = job.CurrentLeg != null && job.CurrentLeg.Origin != null
                ? job.CurrentLeg.Origin.FreightAnchor : null;
            if (anchor == null)
            {
                FreightLogisticsManager.Instance.ReportExecution(
                    job, this, CreateCorrelation(), FreightExecutionReport.Failed,
                    FreightFailureReason.PickupAnchorMissing);
                return;
            }

            StartTrackedRoute(anchor, FreightExecutionReport.PickupRouteStarted,
                FreightFailureReason.PersonnelRouteRunnerMissing,
                FreightFailureReason.PickupRouteUnavailable);
        }

        private void StartDeliveryRoute()
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            Transform anchor = job.CurrentLeg != null && job.CurrentLeg.Destination != null
                ? job.CurrentLeg.Destination.FreightAnchor : null;
            if (anchor == null)
            {
                FreightLogisticsManager.Instance.ReportExecution(
                    job, this, CreateCorrelation(), FreightExecutionReport.Failed,
                    FreightFailureReason.DropoffAnchorMissing);
                return;
            }

            StartTrackedRoute(anchor, FreightExecutionReport.LoadedRouteStarted,
                FreightFailureReason.PersonnelRouteRunnerMissing,
                FreightFailureReason.DropoffRouteUnavailable);
        }

        private void StartReturnToDutyRoute()
        {
            if (!IsActive)
                return;
            WorkplaceComponent workplace = Service != null ? Service.Workplace : null;
            Transform anchor = workplace != null ? workplace.DutyAnchor : null;
            if (anchor == null || routeRunner == null)
            {
                Release();
                return;
            }

            returningToDuty = true;
            retryAtTick = long.MaxValue;
            StartTrackedRoute(anchor, null,
                FreightFailureReason.PersonnelRouteRunnerMissing,
                FreightFailureReason.PersonnelRouteRunnerMissing);
        }

        private bool StartTrackedRoute(
            Transform anchor,
            FreightExecutionReport? freightStartReport,
            FreightFailureReason missingRunnerReason,
            FreightFailureReason unavailableReason)
        {
            if (!IsActive || routeRunner == null || anchor == null)
            {
                if (returningToDuty)
                    HandleReturnRouteFailure("personnel_route_runner_missing");
                else
                    HandleRouteStartFailure(missingRunnerReason, "personnel_route_runner_missing");
                return false;
            }

            routeStartInProgress = true;
            pendingRouteCompletedId = null;
            pendingRouteFailedId = null;
            pendingRouteFailedReason = null;
            bool started = routeRunner.TryStartRoute(anchor, out string routeId, out string failure);
            routeStartInProgress = false;
            PersonnelRoutePlan plan = routeRunner.CurrentPlan;
            if (!started || string.IsNullOrEmpty(routeId) || plan == null)
            {
                ClearPendingRouteEvents();
                string detail = string.IsNullOrEmpty(failure) ? "personnel_route_start_failed" : failure;
                if (returningToDuty)
                    HandleReturnRouteFailure(detail);
                else
                    HandleRouteStartFailure(unavailableReason, detail);
                return false;
            }

            activeRouteId = routeId;
            activeRouteStableId = plan.StableId;
            if (freightStartReport.HasValue)
            {
                FreightLogisticsManager manager = FreightLogisticsManager.Instance;
                if (manager == null || !manager.ReportExecution(
                        job, this, CreateCorrelation(), freightStartReport.Value))
                {
                    routeRunner.StopRoute(routeId);
                    ClearActiveRoute();
                    ClearPendingRouteEvents();
                    return false;
                }
            }

            ProcessPendingRouteEvents(routeId);
            return true;
        }

        private bool ShouldReturnToDuty()
        {
            if (lease == null || lease.HasPendingReleaseRequest || worker == null ||
                Service == null || Service.Workplace == null || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null)
            {
                return false;
            }

            return WorkforceManager.Instance.IsGenuinelyOnDuty(
                worker, Service.Workplace, assignedRole,
                SimulationManager.Instance.CurrentGameHour);
        }

        private bool ShouldResumeFacilityWork()
        {
            ColonistBrain brain = worker != null ? worker.GetComponent<ColonistBrain>() : null;
            if (lease == null || lease.HasPendingReleaseRequest || brain == null ||
                brain.IsCriticallyHungry || brain.State != ColonistBrainState.Working ||
                Service == null || Service.Workplace == null ||
                Service.Workplace.ExecutionMode != WorkplaceExecutionMode.FacilityActivity ||
                WorkforceManager.Instance == null || SimulationManager.Instance == null ||
                !WorkforceManager.Instance.TryGetCurrentDuty(
                    worker, SimulationManager.Instance.CurrentGameHour,
                    out WorkAssignment assignment))
            {
                return false;
            }

            return assignment.Workplace == Service.Workplace && assignment.Role == assignedRole;
        }

        private void ResumeFacilityWork()
        {
            WorkplaceComponent workplace = Service != null ? Service.Workplace : null;
            if (activityRunner == null || workplace == null ||
                !workplace.TryGetRoleBinding(assignedRole, out WorkplaceRoleBinding binding) ||
                workplace.Facility == null || activityRunner.HasActiveRequest ||
                !activityRunner.RequestActivity(workplace.Facility, binding.ActivityId))
            {
                Debug.LogWarning($"{(worker != null ? worker.name : "Worker")}: couldn't resume " +
                    "the assigned workplace activity after walking freight.", worker);
            }
        }

        private void HandleApproachRouteCompleted(string routeId)
        {
            if (routeStartInProgress)
            {
                pendingRouteCompletedId = routeId;
                return;
            }
            if (!IsActive || string.IsNullOrEmpty(activeRouteId) ||
                !string.Equals(activeRouteId, routeId, StringComparison.Ordinal))
                return;

            FreightExecutionCorrelation correlation = CreateCorrelation();
            ClearActiveRoute();
            if (returningToDuty)
            {
                Release();
                return;
            }

            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            if (job.State == FreightJobState.TravelingToPickup)
            {
                if (FreightLogisticsManager.Instance.ReportExecution(
                        job, this, correlation, FreightExecutionReport.ProviderAtSource))
                    StartDeliveryRoute();
            }
            else if (job.State == FreightJobState.TravelingToDropoff)
            {
                FreightLogisticsManager.Instance.ReportExecution(
                    job, this, correlation, FreightExecutionReport.LoadedLegArrived);
            }
        }

        private void HandleApproachRouteFailed(string routeId, string reason)
        {
            if (routeStartInProgress)
            {
                pendingRouteFailedId = routeId;
                pendingRouteFailedReason = reason;
                return;
            }
            if (!IsActive || string.IsNullOrEmpty(activeRouteId) ||
                !string.Equals(activeRouteId, routeId, StringComparison.Ordinal))
                return;

            FreightExecutionCorrelation correlation = CreateCorrelation();
            ClearActiveRoute();
            if (returningToDuty)
            {
                HandleReturnRouteFailure(reason);
                return;
            }

            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            FreightLogisticsManager.Instance.ReportExecution(
                job, this, correlation, FreightExecutionReport.Failed,
                FreightFailureReason.FreightRouteFailed, reason);
        }

        private void HandleRouteStartFailure(FreightFailureReason reason, string detail)
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            FreightLogisticsManager.Instance.ReportExecution(
                job, this, CreateCorrelation(), FreightExecutionReport.Failed, reason, detail);
        }

        private FreightExecutionCorrelation CreateCorrelation()
        {
            return new FreightExecutionCorrelation(
                job != null ? job.Allocation.Id : string.Empty,
                LegIndex,
                ExecutionId,
                activeRouteId,
                activeRouteStableId);
        }

        private void ProcessPendingRouteEvents(string routeId)
        {
            if (string.Equals(pendingRouteCompletedId, routeId, StringComparison.Ordinal))
            {
                ClearPendingRouteEvents();
                HandleApproachRouteCompleted(routeId);
                return;
            }

            if (string.Equals(pendingRouteFailedId, routeId, StringComparison.Ordinal))
            {
                string reason = pendingRouteFailedReason;
                ClearPendingRouteEvents();
                HandleApproachRouteFailed(routeId, reason);
                return;
            }

            ClearPendingRouteEvents();
        }

        private void ClearActiveRoute()
        {
            activeRouteId = string.Empty;
            activeRouteStableId = string.Empty;
        }

        private void ClearPendingRouteEvents()
        {
            pendingRouteCompletedId = null;
            pendingRouteFailedId = null;
            pendingRouteFailedReason = null;
        }

        private void HandleReturnRouteFailure(string reason)
        {
            retryAtTick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick + 5L
                : long.MaxValue;
            Debug.LogWarning($"{worker.name}: return to the Airlock duty anchor failed through Personnel Routing: " +
                (string.IsNullOrWhiteSpace(reason) ? "route_unavailable" : reason), worker);
        }

        private void Release()
        {
            if (!IsActive)
                return;
            if (routeRunner != null)
            {
                routeRunner.ApproachRouteCompleted -= HandleApproachRouteCompleted;
                routeRunner.ApproachRouteFailed -= HandleApproachRouteFailed;
                if (!string.IsNullOrEmpty(activeRouteId))
                    routeRunner.StopRoute(activeRouteId);
            }
            ClearActiveRoute();
            ClearPendingRouteEvents();
            IsActive = false;
            if (lease != null && lease.IsActive)
                lease.Release();
            Service?.ReleaseExecution(this);
        }
    }
}
