using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColonistMotor), typeof(ColonistActivityRunner))]
    [AddComponentMenu("Colony/Logistics/Walking Freight Runner")]
    public sealed class WalkingFreightRunner : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private ColonistMotor motor;
        private WalkingFreightCarrierComponent carrier;
        private FreightDeliveryJob job;

        public int SimulationTickPriority => 290;
        public FreightDeliveryJob CurrentJob => job;

        private void Awake()
        {
            motor = GetComponent<ColonistMotor>();
            carrier = GetComponent<WalkingFreightCarrierComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
            if (motor == null)
                motor = GetComponent<ColonistMotor>();
            if (motor != null)
            {
                motor.Arrived += HandleArrived;
                motor.MoveFailed += HandleMoveFailed;
            }
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (motor != null)
            {
                motor.Arrived -= HandleArrived;
                motor.MoveFailed -= HandleMoveFailed;
            }
        }

        public bool Begin(FreightDeliveryJob assignedJob)
        {
            if (assignedJob == null || job != null || motor == null ||
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
                if (job.IsEmergencyExcursion)
                {
                    if (carrier.Brain == null || carrier.Brain.ShouldAbortWorkExcursionBeforePickup)
                    {
                        manager.CancelBeforePickup(job, "work_excursion_no_longer_authorized");
                        return;
                    }
                    if (!carrier.Brain.WorkExcursionReady)
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
            if (!motor.MoveTo(job.Source.FreightAnchor) && job != null && !job.IsTerminal)
                HandleMoveFailed("pickup_route_unavailable");
        }

        private void StartDeliveryRoute()
        {
            if (job == null || job.Destination == null || job.Destination.FreightAnchor == null)
            {
                FreightLogisticsManager.Instance.BlockJob(job, "dropoff_anchor_missing");
                return;
            }

            FreightLogisticsManager.Instance.SetJobState(job, FreightJobState.TravelingToDropoff);
            if (!motor.MoveTo(job.Destination.FreightAnchor) && job != null && !job.IsTerminal)
                HandleMoveFailed("dropoff_route_unavailable");
        }

        private void RetryDeliveryRoute()
        {
            if (job == null || !job.HasPickedUp)
                return;
            FreightLogisticsManager.Instance.ResumeJob(job, FreightJobState.TravelingToDropoff);
            StartDeliveryRoute();
        }

        private void HandleArrived()
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;

            FreightLogisticsManager manager = FreightLogisticsManager.Instance;
            if (job.State == FreightJobState.TravelingToPickup)
            {
                if (manager.TryPickup(job))
                    StartDeliveryRoute();
            }
            else if (job.State == FreightJobState.TravelingToDropoff)
            {
                manager.TryDeliver(job);
            }
        }

        private void HandleMoveFailed(string reason)
        {
            if (job == null || FreightLogisticsManager.Instance == null)
                return;

            if (job.HasPickedUp)
                FreightLogisticsManager.Instance.BlockJob(job, reason);
            else
                FreightLogisticsManager.Instance.CancelBeforePickup(job, reason);
        }
    }
}
