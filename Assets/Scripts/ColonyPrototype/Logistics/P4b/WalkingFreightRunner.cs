using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColonistMotor), typeof(ColonistActivityRunner), typeof(PersonnelRouteRunner))]
    [AddComponentMenu("Colony/Logistics/Walking Freight Runner")]
    public sealed class WalkingFreightRunner : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private PersonnelRouteRunner routeRunner;
        private WalkingFreightCarrierComponent carrier;
        private FreightDeliveryJob job;

        public int SimulationTickPriority => 290;
        public FreightDeliveryJob CurrentJob => job;

        private void Awake()
        {
            routeRunner = GetComponent<PersonnelRouteRunner>();
            carrier = GetComponent<WalkingFreightCarrierComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
            if (routeRunner == null)
                routeRunner = GetComponent<PersonnelRouteRunner>();
            if (routeRunner != null)
            {
                routeRunner.RouteCompleted += HandleRouteCompleted;
                routeRunner.RouteFailed += HandleRouteFailed;
            }
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (routeRunner != null)
            {
                routeRunner.RouteCompleted -= HandleRouteCompleted;
                routeRunner.RouteFailed -= HandleRouteFailed;
            }
        }

        public bool Begin(FreightDeliveryJob assignedJob)
        {
            if (assignedJob == null || job != null || routeRunner == null ||
                FreightLogisticsManager.Instance == null)
                return false;
            job = assignedJob;
            return true;
        }

        public void Clear(FreightDeliveryJob completedJob)
        {
            if (job == completedJob)
                job = null;
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (job == null || job.IsTerminal || FreightLogisticsManager.Instance == null)
                return;

            FreightLogisticsManager manager = FreightLogisticsManager.Instance;
            if (job.State == FreightJobState.Assigned)
            {
                if (job.IsEmergencyWork)
                {
                    manager.CancelBeforePickup(job, "emergency_work_is_owned_by_workplace_service");
                    return;
                }
                else if (!IsRoutineDutyStillValid())
                {
                    manager.CancelBeforePickup(job, "carrier_no_longer_on_duty");
                    return;
                }

                StartPickupRoute();
                return;
            }

            if (job.State == FreightJobState.Blocked &&
                SimulationManager.Instance != null &&
                SimulationManager.Instance.CurrentTick >= job.RetryAtTick)
            {
                RetryDeliveryRoute();
            }
        }

        private bool IsRoutineDutyStillValid()
        {
            if (carrier == null || carrier.Identity == null ||
                WorkforceManager.Instance == null || SimulationManager.Instance == null ||
                !WorkforceManager.Instance.TryGetCurrentDuty(
                    carrier.Identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out WorkAssignment assignment))
                return false;

            return assignment.Workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty &&
                   carrier.Brain != null &&
                   (carrier.Brain.State == ColonistBrainState.Working || job.HasPickedUp);
        }

        private void StartPickupRoute()
        {
            if (job.Source == null || job.Source.FreightAnchor == null)
            {
                FreightLogisticsManager.Instance.CancelBeforePickup(job, "pickup_anchor_missing");
                return;
            }

            FreightLogisticsManager.Instance.SetJobState(job, FreightJobState.TravelingToPickup);
            string reason = "personnel_route_runner_missing";
            if (routeRunner == null ||
                !routeRunner.TryStartRoute(job.Source.FreightAnchor, out reason))
                HandleRouteStartFailure(job, reason, "pickup_route_unavailable");
        }

        private void StartDeliveryRoute()
        {
            if (job == null || job.Destination == null || job.Destination.FreightAnchor == null)
            {
                FreightLogisticsManager.Instance.BlockJob(job, "dropoff_anchor_missing");
                return;
            }

            FreightLogisticsManager.Instance.SetJobState(job, FreightJobState.TravelingToDropoff);
            string reason = "personnel_route_runner_missing";
            if (routeRunner == null ||
                !routeRunner.TryStartRoute(job.Destination.FreightAnchor, out reason))
                HandleRouteStartFailure(job, reason, "dropoff_route_unavailable");
        }

        private void RetryDeliveryRoute()
        {
            if (job == null || !job.HasPickedUp)
                return;
            FreightLogisticsManager.Instance.ResumeJob(job, FreightJobState.TravelingToDropoff);
            StartDeliveryRoute();
        }

        private void HandleRouteCompleted(PersonnelRoutePlan plan)
        {
            if (job == null || plan == null || plan.Person != carrier.Identity ||
                FreightLogisticsManager.Instance == null)
                return;

            FreightLogisticsManager manager = FreightLogisticsManager.Instance;
            if (job.State == FreightJobState.TravelingToPickup)
            {
                if (plan.FinalDestination != job.Source.FreightAnchor)
                    return;
                if (manager.TryPickup(job))
                    StartDeliveryRoute();
            }
            else if (job.State == FreightJobState.TravelingToDropoff)
            {
                if (plan.FinalDestination != job.Destination.FreightAnchor)
                    return;
                manager.TryDeliver(job);
            }
        }

        private void HandleRouteFailed(PersonnelRoutePlan plan, string reason)
        {
            if (job == null || plan == null || plan.Person != carrier.Identity ||
                FreightLogisticsManager.Instance == null)
                return;

            Transform expected = job.State == FreightJobState.TravelingToPickup
                ? (job.Source != null ? job.Source.FreightAnchor : null)
                : (job.Destination != null ? job.Destination.FreightAnchor : null);
            if (plan.FinalDestination != expected)
                return;

            HandleRouteStartFailure(job, reason, "freight_route_failed");
        }

        private void HandleRouteStartFailure(FreightDeliveryJob failedJob,
            string reason, string fallbackReason)
        {
            if (failedJob == null || job != failedJob || FreightLogisticsManager.Instance == null)
                return;
            string failure = string.IsNullOrWhiteSpace(reason) ? fallbackReason : reason;
            if (job.HasPickedUp)
                FreightLogisticsManager.Instance.BlockJob(job, failure);
            else
                FreightLogisticsManager.Instance.CancelBeforePickup(job, failure);
        }
    }
}
