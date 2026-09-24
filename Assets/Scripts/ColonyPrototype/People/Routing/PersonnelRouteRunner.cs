using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum PersonnelRouteExecutionState
    {
        Idle,
        Executing,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Executes one policy-free PersonnelRoutePlan for one colonist. It owns no
    /// activity, hunger, sleep, work, or freight decisions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColonistIdentity), typeof(ColonistMotor))]
    [AddComponentMenu("Colony/Navigation/Personnel Route Runner")]
    public sealed class PersonnelRouteRunner : MonoBehaviour, IActivityApproachRouter
    {
        [SerializeField] private PersonnelRoutingManager routingManager;
        [SerializeField] private ColonistIdentity person;
        [SerializeField] private ColonistMotor motor;
        [SerializeField] private ColonistActivityRunner activityRunner;

        private PersonnelRoutePlan currentPlan;
        private int currentLegIndex = -1;
        private bool motorEventsSubscribed;

        public ColonistIdentity Person => person;
        public Transform FinalDestination => currentPlan != null
            ? currentPlan.FinalDestination
            : null;
        public PersonnelRouteExecutionState State { get; private set; } =
            PersonnelRouteExecutionState.Idle;
        public string Status => State.ToString();
        public PersonnelRoutePlan CurrentPlan => currentPlan;
        public int CurrentLegIndex => currentLegIndex;
        public PersonnelRouteLeg CurrentLeg =>
            currentPlan != null && currentLegIndex >= 0 &&
            currentLegIndex < currentPlan.Legs.Count
                ? currentPlan.Legs[currentLegIndex]
                : null;
        public PersonnelRouteLegType? CurrentLegType => CurrentLeg?.Type;
        public float CurrentLegEstimatedDistance => CurrentLeg != null
            ? CurrentLeg.EstimatedDistance
            : 0f;
        public string LastFailureReason { get; private set; } = string.Empty;
        public bool IsExecuting => State == PersonnelRouteExecutionState.Executing;

        public event Action<PersonnelRoutePlan> RouteCompleted;
        public event Action<PersonnelRoutePlan, string> RouteFailed;

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            SubscribeMotorEvents();
            BindActivityRunner();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeMotorEvents();
            BindActivityRunner();
        }

        private void OnDisable()
        {
            UnsubscribeMotorEvents();
            if (IsExecuting)
            {
                motor?.Stop();
                State = PersonnelRouteExecutionState.Cancelled;
                currentLegIndex = -1;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeMotorEvents();
            if (activityRunner != null && activityRunner.ApproachRouter == this)
                activityRunner.SetApproachRouter(null);
        }

        public bool TryStartRoute(Transform destination, out string failureReason)
        {
            ResolveReferences();
            PersonnelRoutingManager manager = ResolveRoutingManager();
            if (manager == null)
            {
                failureReason = PersonnelRoutingManager.MissingRoutingManagerReason;
                return false;
            }

            if (!manager.TryPlanRoute(
                    person,
                    destination,
                    out PersonnelRoutePlan plan,
                    out failureReason))
            {
                return false;
            }

            return BeginRoute(plan, out failureReason);
        }

        public bool BeginRoute(
            PersonnelRoutePlan plan,
            out string failureReason)
        {
            ResolveReferences();
            if (IsExecuting)
            {
                failureReason = "personnel_route_already_executing";
                return false;
            }
            if (plan == null || person == null || plan.Person != person)
            {
                failureReason = "personnel_route_identity_mismatch";
                return false;
            }

            currentPlan = plan;
            currentLegIndex = 0;
            LastFailureReason = string.Empty;
            State = PersonnelRouteExecutionState.Executing;
            if (StartCurrentLeg())
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = string.IsNullOrEmpty(LastFailureReason)
                ? "personnel_route_start_failed"
                : LastFailureReason;
            return false;
        }

        public void StopRoute()
        {
            if (!IsExecuting)
                return;

            motor?.Stop();
            State = PersonnelRouteExecutionState.Cancelled;
            currentLegIndex = -1;
            LastFailureReason = string.Empty;
        }

        private bool StartCurrentLeg()
        {
            PersonnelRouteLeg leg = CurrentLeg;
            if (leg == null)
            {
                Fail("personnel_route_leg_missing");
                return false;
            }
            if (leg.Type != PersonnelRouteLegType.Walk)
            {
                Fail("personnel_route_leg_not_supported");
                return false;
            }
            if (motor == null)
            {
                Fail("personnel_route_motor_missing");
                return false;
            }

            bool accepted = motor.MoveTo(leg.Destination);
            if (!accepted && IsExecuting)
                Fail("personnel_route_motor_rejected_destination");
            return accepted && IsExecuting;
        }

        private void HandleMotorArrived()
        {
            if (!IsExecuting || currentPlan == null)
                return;

            currentLegIndex++;
            if (currentLegIndex < currentPlan.Legs.Count)
            {
                if (!StartCurrentLeg())
                    return;
                return;
            }

            currentLegIndex = Math.Max(0, currentPlan.Legs.Count - 1);
            State = PersonnelRouteExecutionState.Completed;
            PersonnelRoutePlan completed = currentPlan;
            RouteCompleted?.Invoke(completed);
        }

        private void HandleMotorFailed(string reason)
        {
            if (IsExecuting)
                Fail(reason);
        }

        private void Fail(string reason)
        {
            State = PersonnelRouteExecutionState.Failed;
            LastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "personnel_route_failed"
                : reason;
            PersonnelRoutePlan failed = currentPlan;
            RouteFailed?.Invoke(failed, LastFailureReason);
        }

        private void ResolveReferences()
        {
            if (routingManager == null)
                routingManager = PersonnelRoutingManager.Instance;
            if (person == null)
                person = GetComponent<ColonistIdentity>();
            if (motor == null)
                motor = GetComponent<ColonistMotor>();
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();
        }

        private PersonnelRoutingManager ResolveRoutingManager()
        {
            if (routingManager == null)
                routingManager = PersonnelRoutingManager.Instance;
            return routingManager;
        }

        private void BindActivityRunner()
        {
            if (activityRunner != null && activityRunner.ApproachRouter != this)
                activityRunner.SetApproachRouter(this);
        }

        private void SubscribeMotorEvents()
        {
            if (motorEventsSubscribed || motor == null)
                return;

            motor.Arrived += HandleMotorArrived;
            motor.MoveFailed += HandleMotorFailed;
            motorEventsSubscribed = true;
        }

        private void UnsubscribeMotorEvents()
        {
            if (!motorEventsSubscribed || motor == null)
                return;

            motor.Arrived -= HandleMotorArrived;
            motor.MoveFailed -= HandleMotorFailed;
            motorEventsSubscribed = false;
        }
    }
}
