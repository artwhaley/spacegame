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

        internal WalkingFreightExecution(
            WalkingFreightWorkService service,
            ColonistIdentity worker,
            WorkExecutionLease lease,
            JobRoleDefinition assignedRole,
            float capacity,
            float positioningDistance,
            float loadedCargoTravelCost,
            bool emergency)
        {
            Service = service;
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
                routeRunner.RouteCompleted += HandleRouteCompleted;
                routeRunner.RouteFailed += HandleRouteFailed;
            }
        }

        public WalkingFreightWorkService Service { get; }
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
                        job, this, lease.PendingReleaseReason ?? WorkReleaseReason.OtherWorkPolicy);
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
                ? manager.RequestExecutionRelease(job, this, reason)
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
                    job, this, FreightExecutionReport.Failed, FreightFailureReason.PickupAnchorMissing);
                return;
            }

            string failure = "personnel_route_runner_missing";
            if (routeRunner == null)
            {
                HandleRouteStartFailure(FreightFailureReason.PersonnelRouteRunnerMissing, failure);
                return;
            }
            if (!routeRunner.TryStartRoute(anchor, out failure))
            {
                HandleRouteStartFailure(FreightFailureReason.PickupRouteUnavailable, failure);
                return;
            }
            FreightLogisticsManager.Instance.ReportExecution(
                job, this, FreightExecutionReport.PickupRouteStarted);
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
                    job, this, FreightExecutionReport.Failed, FreightFailureReason.DropoffAnchorMissing);
                return;
            }

            string failure = "personnel_route_runner_missing";
            if (routeRunner == null)
            {
                HandleRouteStartFailure(FreightFailureReason.PersonnelRouteRunnerMissing, failure);
                return;
            }
            if (!routeRunner.TryStartRoute(anchor, out failure))
            {
                HandleRouteStartFailure(FreightFailureReason.DropoffRouteUnavailable, failure);
                return;
            }
            FreightLogisticsManager.Instance.ReportExecution(
                job, this, FreightExecutionReport.LoadedRouteStarted);
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
            string failure = "personnel_route_runner_missing";
            if (!routeRunner.TryStartRoute(anchor, out failure))
                HandleReturnRouteFailure(failure);
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

        private void HandleRouteCompleted(PersonnelRoutePlan plan)
        {
            if (!IsActive || plan == null || plan.Person != worker)
                return;

            if (returningToDuty)
            {
                Transform dutyAnchor = Service != null && Service.Workplace != null
                    ? Service.Workplace.DutyAnchor : null;
                if (plan.FinalDestination == dutyAnchor)
                    Release();
                return;
            }

            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            LogisticsRouteLeg leg = job.CurrentLeg;
            if (job.State == FreightJobState.TravelingToPickup &&
                leg != null && leg.Origin != null && plan.FinalDestination == leg.Origin.FreightAnchor)
            {
                if (FreightLogisticsManager.Instance.ReportExecution(
                        job, this, FreightExecutionReport.ProviderAtSource))
                    StartDeliveryRoute();
            }
            else if (job.State == FreightJobState.TravelingToDropoff &&
                     leg != null && leg.Destination != null &&
                     plan.FinalDestination == leg.Destination.FreightAnchor)
            {
                FreightLogisticsManager.Instance.ReportExecution(
                    job, this, FreightExecutionReport.LoadedLegArrived);
            }
        }

        private void HandleRouteFailed(PersonnelRoutePlan plan, string reason)
        {
            if (!IsActive || plan == null || plan.Person != worker)
                return;

            if (returningToDuty)
            {
                HandleReturnRouteFailure(reason);
                return;
            }

            if (job == null)
                return;
            LogisticsRouteLeg leg = job.CurrentLeg;
            Transform expected = job.State == FreightJobState.TravelingToPickup
                ? leg != null && leg.Origin != null ? leg.Origin.FreightAnchor : null
                : leg != null && leg.Destination != null ? leg.Destination.FreightAnchor : null;
            if (plan.FinalDestination == expected)
                HandleRouteStartFailure(FreightFailureReason.FreightRouteFailed, reason);
        }

        private void HandleRouteStartFailure(FreightFailureReason reason, string detail)
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;
            FreightLogisticsManager.Instance.ReportExecution(
                job, this, FreightExecutionReport.Failed, reason, detail);
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
                routeRunner.RouteCompleted -= HandleRouteCompleted;
                routeRunner.RouteFailed -= HandleRouteFailed;
                routeRunner.StopRoute();
            }
            IsActive = false;
            if (lease != null && lease.IsActive)
                lease.Release();
            Service?.ReleaseExecution(this);
        }
    }
}
