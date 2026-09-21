using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum ColonistBrainState
    {
        Idle,
        SleepSeeking,
        Sleeping,
        WorkSeeking,
        Working,
        EatSeeking,
        Eating,
        OffDutySeeking,
        OffDutyActive
    }

    [DisallowMultipleComponent]
    public sealed class ColonistBrain : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float ObligationWakeLeadGameHours = 0.5f;

        [SerializeField] private ColonistStatsComponent stats;
        [SerializeField] private ColonistIdentity identity;
        [SerializeField] private ColonistTargetResolver targetResolver;
        [SerializeField] private ColonistActivityRunner activityRunner;
        [SerializeField] private ColonistBrainState state = ColonistBrainState.Idle;

        private ActivityTarget sleepTargetInProgress;
        private ActivityTarget workTargetInProgress;
        private ActivityTarget eatTargetInProgress;
        private OffDutyOpportunity opportunityInProgress;
        private float actualActiveOffDutyGameHours;
        private bool wakeRequested;
        private bool workStopRequested;
        private bool eatStopRequested;
        private bool offDutyStopRequested;
        private string lastDecision = "idle";
        private string lastDecisionReason = "initial_state";
        private string lastDecisionSignature;
        private ColonistFreeTimePlan latestFreeTimePlan;

        public ColonistBrainState State => state;
        public int SimulationTickPriority => 100;
        public OffDutyOpportunity OpportunityInProgress => opportunityInProgress;
        public OffDutyOpportunity OffDutyOpportunity => opportunityInProgress;
        public float ActualActiveOffDutyGameHours => actualActiveOffDutyGameHours;
        public bool OffDutyStopRequested => offDutyStopRequested;
        public string LastDecision => lastDecision;
        public string LastDecisionReason => lastDecisionReason;
        public OffDutyOpportunity OffDutyTarget => opportunityInProgress;
        public float OffDutyPlannedDuration =>
            opportunityInProgress != null ? opportunityInProgress.PlannedDurationGameHours : 0f;
        public float OffDutyActiveDuration => actualActiveOffDutyGameHours;
        public ScheduledWorkOccurrence NextWork
        {
            get
            {
                return TryGetNextWork(out ScheduledWorkOccurrence occurrence)
                    ? occurrence
                    : null;
            }
        }
        public float TimeUntilWork
        {
            get
            {
                if (SimulationManager.Instance == null || identity == null)
                    return float.PositiveInfinity;

                float currentHour = SimulationManager.Instance.CurrentGameHour;
                if (HasCurrentWorkObligation())
                    return 0f;

                return TryGetNextWork(out ScheduledWorkOccurrence occurrence)
                    ? occurrence.TimeUntilStart(currentHour)
                    : float.PositiveInfinity;
            }
        }
        public float ProtectedSleepRequired =>
            latestFreeTimePlan != null ? latestFreeTimePlan.RequiredProtectedSleepDuration : 0f;
        public float MaximumDiscretionaryDuration =>
            latestFreeTimePlan != null ? latestFreeTimePlan.MaximumSafeDiscretionaryDuration : 0f;

        private void Awake()
        {
            if (stats == null)
                stats = GetComponent<ColonistStatsComponent>();
            if (identity == null)
                identity = GetComponent<ColonistIdentity>();
            if (targetResolver == null)
                targetResolver = GetComponent<ColonistTargetResolver>();
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();

            if (activityRunner != null)
                activityRunner.ActivityLifecycleChanged += HandleActivityLifecycleChanged;

            if (!HasDependencies())
            {
                Debug.LogError(
                    $"{name}: ColonistBrain requires ColonistStatsComponent, " +
                    "ColonistIdentity, ColonistTargetResolver, and " +
                    "ColonistActivityRunner on the same GameObject.",
                    this);
            }
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDestroy()
        {
            if (activityRunner != null)
                activityRunner.ActivityLifecycleChanged -= HandleActivityLifecycleChanged;
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (deltaGameHours <= 0f ||
                float.IsNaN(deltaGameHours) ||
                float.IsInfinity(deltaGameHours) ||
                !HasDependencies())
            {
                return;
            }

            switch (state)
            {
                case ColonistBrainState.Idle:
                    TickIdle();
                    break;
                case ColonistBrainState.SleepSeeking:
                    TickSleepSeeking();
                    break;
                case ColonistBrainState.Sleeping:
                    TickSleeping();
                    break;
                case ColonistBrainState.WorkSeeking:
                    TickWorkSeeking();
                    break;
                case ColonistBrainState.Working:
                    TickWorking();
                    break;
                case ColonistBrainState.EatSeeking:
                    TickEatSeeking();
                    break;
                case ColonistBrainState.Eating:
                    TickEating();
                    break;
                case ColonistBrainState.OffDutySeeking:
                    TickOffDutySeeking();
                    break;
                case ColonistBrainState.OffDutyActive:
                    TickOffDutyActive(deltaGameHours);
                    break;
            }
        }

        private void TickIdle()
        {
            if (activityRunner.HasActiveRequest)
                return;

            if (stats.IsCriticallyHungry)
            {
                RecordDecision(
                    "colonist.decision.eat",
                    "critical_hunger",
                    null,
                    new SimulationLogField("hunger", stats.Hunger));
                if (TryStartEat())
                    return;
            }

            if (HasCurrentWorkObligation())
            {
                RecordDecision("colonist.decision.work", "current_work");
                TryStartWork();
                return;
            }

            if (stats.IsSleepy)
            {
                if (HasWorkStartingWithinPreparationWindow())
                {
                    RecordDecision(
                        "colonist.decision.blocked",
                        "sleep_blocked_by_work_preparation");
                    return;
                }

                RecordDecision("colonist.decision.sleep", "sleepy");
                TryStartSleep();
                return;
            }

            if (stats.IsHungry)
            {
                RecordDecision(
                    "colonist.decision.eat",
                    "hungry",
                    null,
                    new SimulationLogField("hunger", stats.Hunger));
                TryStartEat();
                return;
            }

            if (HasWorkStartingWithinPreparationWindow())
            {
                RecordDecision(
                    "colonist.decision.blocked",
                    "work_preparation_window");
                return;
            }

            if (stats.ShouldPreferRest)
            {
                RecordDecision("colonist.decision.sleep", "rest_preferred");
                TryStartSleep();
                return;
            }

            TryStartOffDuty();
        }

        private void TickSleepSeeking()
        {
            if (!IsTargetValid(sleepTargetInProgress))
            {
                FinishSleepLifecycle();
                return;
            }

            if (!wakeRequested &&
                (HasCurrentWorkObligation() ||
                 HasWorkStartingWithinPreparationWindow() ||
                 stats.IsCriticallyHungry))
            {
                RequestWake();
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(
                    activityRunner.ActiveActivityId,
                    sleepTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                state = ColonistBrainState.Sleeping;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishSleepLifecycle();
        }

        private void TickSleeping()
        {
            if (!IsTargetValid(sleepTargetInProgress))
            {
                FinishSleepLifecycle();
                return;
            }

            if (!wakeRequested &&
                (stats.Fatigue <= 0f ||
                 HasCurrentWorkObligation() ||
                 HasWorkStartingWithinPreparationWindow() ||
                 stats.IsCriticallyHungry))
            {
                RequestWake();
            }

            if (wakeRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishSleepLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishSleepLifecycle();
        }

        private void RequestWake()
        {
            if (wakeRequested)
                return;

            wakeRequested = true;
            RecordDecision(
                "colonist.decision.interrupted",
                stats.IsCriticallyHungry ? "critical_hunger" :
                HasCurrentWorkObligation() ? "current_work" : "work_preparation");
            activityRunner.Stop();
        }

        private void FinishSleepLifecycle()
        {
            wakeRequested = false;
            sleepTargetInProgress = null;
            state = ColonistBrainState.Idle;
        }

        private bool TryStartSleep()
        {
            if (HasWorkStartingWithinPreparationWindow() ||
                !targetResolver.TryResolveTarget(
                    ActivityPurpose.Sleep,
                    out ActivityTarget target) ||
                !IsTargetValid(target) ||
                !activityRunner.RequestActivity(target.Facility, target.ActivityId))
            {
                return false;
            }

            sleepTargetInProgress = target;
            wakeRequested = false;
            state = ColonistBrainState.SleepSeeking;
            return true;
        }

        private void TickWorkSeeking()
        {
            if (stats.IsCriticallyHungry)
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (workTargetInProgress == null || !workTargetInProgress.IsConfigured ||
                !HasCurrentWorkObligation())
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(
                    activityRunner.ActiveActivityId,
                    workTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                state = ColonistBrainState.Working;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishWorkLifecycle();
        }

        private void TickWorking()
        {
            if (workTargetInProgress == null || !workTargetInProgress.IsConfigured)
            {
                RequestWorkStop();
            }
            else if (!workStopRequested &&
                     (stats.IsCriticallyHungry ||
                      !HasCurrentWorkObligation() ||
                      !IsCurrentWorkActivity()))
            {
                RequestWorkStop();
            }

            if (workStopRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishWorkLifecycle();
        }

        private bool TryStartWork()
        {
            if (!targetResolver.TryResolveTarget(
                    ActivityPurpose.Work,
                    out ActivityTarget target) ||
                !IsTargetValid(target) ||
                !activityRunner.RequestActivity(target.Facility, target.ActivityId))
            {
                RecordDecision("colonist.decision.blocked", "work_target_unavailable");
                return false;
            }

            workTargetInProgress = target;
            workStopRequested = false;
            state = ColonistBrainState.WorkSeeking;
            return true;
        }

        private void RequestWorkStop()
        {
            if (workStopRequested)
                return;

            workStopRequested = true;
            RecordDecision(
                stats.IsCriticallyHungry
                    ? "colonist.decision.interrupted"
                    : "colonist.decision.reconsidered",
                stats.IsCriticallyHungry ? "critical_hunger" : "work_ended");
            if (stats.IsCriticallyHungry)
            {
                SimulationLogManager.RecordEvent(
                    "work.left_for_critical_need",
                    "Work",
                    "Warning",
                    this,
                    workTargetInProgress != null ? workTargetInProgress.Facility : null,
                    new SimulationLogField("reason", "critical_hunger"),
                    new SimulationLogField("hunger", stats.Hunger));
            }
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool IsCurrentWorkActivity()
        {
            return activityRunner.HasActiveRequest &&
                   workTargetInProgress != null &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       workTargetInProgress.ActivityId,
                       StringComparison.Ordinal);
        }

        private void FinishWorkLifecycle()
        {
            workTargetInProgress = null;
            workStopRequested = false;
            state = ColonistBrainState.Idle;
        }

        private void TickEatSeeking()
        {
            if (!IsFoodTargetValid(eatTargetInProgress) ||
                (HasCurrentWorkObligation() && !stats.IsCriticallyHungry))
            {
                RequestEatStop();
                if (!activityRunner.HasActiveRequest)
                    FinishEatLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(
                    activityRunner.ActiveActivityId,
                    eatTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                state = ColonistBrainState.Eating;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishEatLifecycle();
        }

        private void TickEating()
        {
            if (!IsFoodTargetValid(eatTargetInProgress) ||
                !IsCurrentEatActivity() ||
                stats.Hunger <= 0f ||
                (HasCurrentWorkObligation() && !stats.IsCriticallyHungry))
            {
                RequestEatStop();
            }

            if (eatStopRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishEatLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishEatLifecycle();
        }

        private bool TryStartEat()
        {
            if (!stats.IsHungry ||
                !targetResolver.TryResolveTarget(
                    ActivityPurpose.Eat,
                    out ActivityTarget target) ||
                !IsFoodTargetValid(target) ||
                !activityRunner.RequestActivity(target.Facility, target.ActivityId))
            {
                RecordDecision("colonist.decision.blocked", "food_target_unavailable");
                return false;
            }

            eatTargetInProgress = target;
            eatStopRequested = false;
            state = ColonistBrainState.EatSeeking;
            return true;
        }

        private void RequestEatStop()
        {
            if (eatStopRequested)
                return;

            eatStopRequested = true;
            RecordDecision(
                "colonist.decision.interrupted",
                HasCurrentWorkObligation() ? "work_started" :
                stats.IsCriticallyHungry ? "critical_hunger_satisfied" : "hunger_satisfied");
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool IsCurrentEatActivity()
        {
            return activityRunner.HasActiveRequest &&
                   eatTargetInProgress != null &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       eatTargetInProgress.ActivityId,
                       StringComparison.Ordinal);
        }

        private void FinishEatLifecycle()
        {
            eatTargetInProgress = null;
            eatStopRequested = false;
            state = ColonistBrainState.Idle;
        }

        private void TryStartOffDuty()
        {
            if (OffDutyManager.Instance == null || SimulationManager.Instance == null)
            {
                RecordDecision("offduty.no_target", "manager_unavailable");
                return;
            }

            latestFreeTimePlan = ColonistFreeTimePlanner.Calculate(
                stats,
                identity,
                GetComponent<ColonistAssignments>(),
                SimulationManager.Instance.CurrentGameHour);
            if (!latestFreeTimePlan.HasBudget)
            {
                RecordDecision(
                    "colonist.decision.blocked",
                    latestFreeTimePlan.Reason,
                    null,
                    new SimulationLogField(
                        "maximumDuration",
                        latestFreeTimePlan.MaximumSafeDiscretionaryDuration));
                return;
            }

            if (!OffDutyManager.Instance.TryFindOpportunity(
                    new OffDutyQuery(transform.position, latestFreeTimePlan.MaximumSafeDiscretionaryDuration),
                    out OffDutyOpportunity opportunity))
            {
                RecordDecision("offduty.no_target", "no_fitting_opportunity");
                return;
            }

            opportunityInProgress = opportunity;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            if (!activityRunner.RequestActivity(
                    opportunity.Target.Facility,
                    opportunity.Target.ActivityId))
            {
                opportunityInProgress = null;
                RecordDecision("colonist.decision.blocked", "offduty_request_failed");
                return;
            }

            state = ColonistBrainState.OffDutySeeking;
            RecordDecision(
                "colonist.decision.offduty",
                "nearest_fitting_opportunity",
                opportunity.Target.Facility,
                new SimulationLogField("activityId", opportunity.Target.ActivityId),
                new SimulationLogField("duration", opportunity.PlannedDurationGameHours));
            SimulationLogManager.RecordEvent(
                "offduty.target_selected",
                "OffDuty",
                "Info",
                this,
                opportunity.Target.Facility,
                new SimulationLogField("activityId", opportunity.Target.ActivityId),
                new SimulationLogField("reason", "nearest_fitting_opportunity"),
                new SimulationLogField("duration", opportunity.PlannedDurationGameHours));
        }

        private void TickOffDutySeeking()
        {
            if (!IsOffDutyOpportunityLive() ||
                HasCurrentWorkObligation() ||
                HasWorkStartingWithinPreparationWindow() ||
                stats.IsHungry ||
                stats.IsSleepy ||
                stats.IsCriticallyHungry)
            {
                RequestOffDutyStop(
                    stats.IsCriticallyHungry ? "critical_hunger" :
                    stats.IsSleepy ? "sleepy" :
                    stats.IsHungry ? "hungry" :
                    HasCurrentWorkObligation() ? "current_work" :
                    HasWorkStartingWithinPreparationWindow() ? "work_preparation" :
                    "provider_unavailable");
                if (!activityRunner.HasActiveRequest)
                    FinishOffDutyLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive && IsCurrentOffDutyActivity())
            {
                state = ColonistBrainState.OffDutyActive;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishOffDutyLifecycle();
        }

        private void TickOffDutyActive(float deltaGameHours)
        {
            if (!IsOffDutyOpportunityLive() ||
                stats.IsCriticallyHungry ||
                stats.IsHungry ||
                stats.IsSleepy ||
                HasCurrentWorkObligation() ||
                HasWorkStartingWithinPreparationWindow())
            {
                RequestOffDutyStop(
                    stats.IsCriticallyHungry ? "critical_hunger" :
                    stats.IsSleepy ? "sleepy" :
                    stats.IsHungry ? "hungry" :
                    HasCurrentWorkObligation() ? "current_work" :
                    HasWorkStartingWithinPreparationWindow() ? "work_preparation" :
                    "required_staff_unavailable");
            }
            else
            {
                actualActiveOffDutyGameHours += deltaGameHours;
                if (actualActiveOffDutyGameHours >= opportunityInProgress.PlannedDurationGameHours)
                {
                    RecordDecision(
                        "colonist.decision.reconsidered",
                        "planned_duration_complete",
                        opportunityInProgress.Target.Facility);
                    RequestOffDutyStop("planned_duration_complete");
                }
            }

            if (offDutyStopRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishOffDutyLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishOffDutyLifecycle();
        }

        private void RequestOffDutyStop(string reason)
        {
            if (offDutyStopRequested)
                return;

            offDutyStopRequested = true;
            RecordDecision(
                reason == "planned_duration_complete"
                    ? "colonist.decision.reconsidered"
                    : "colonist.decision.interrupted",
                reason,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null);
            SimulationLogManager.RecordEvent(
                reason == "planned_duration_complete"
                    ? "offduty.completed"
                    : "offduty.interrupted",
                "OffDuty",
                "Info",
                this,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null,
                new SimulationLogField("reason", reason),
                new SimulationLogField("activeDuration", actualActiveOffDutyGameHours));
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private void FinishOffDutyLifecycle()
        {
            opportunityInProgress = null;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            state = ColonistBrainState.Idle;
        }

        private bool IsCurrentOffDutyActivity()
        {
            return opportunityInProgress != null &&
                   activityRunner.HasActiveRequest &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       opportunityInProgress.Target.ActivityId,
                       StringComparison.Ordinal);
        }

        private bool IsOffDutyOpportunityLive()
        {
            if (opportunityInProgress == null ||
                opportunityInProgress.Provider == null ||
                !opportunityInProgress.Provider.IsLive(opportunityInProgress.Activity))
            {
                return false;
            }

            OffDutyActivityBinding activity = opportunityInProgress.Activity;
            if (!activity.RequiresStaff)
                return true;

            return WorkforceManager.Instance != null &&
                   WorkforceManager.Instance.HasEnoughActiveWorkers(
                       activity.RequiredWorkplace,
                       activity.RequiredRole,
                       activity.MinimumActiveWorkers,
                       0f,
                       SimulationManager.Instance != null
                           ? SimulationManager.Instance.CurrentGameHour
                           : 0f);
        }

        private bool HasUpcomingObligationWithin(float gameHours)
        {
            return HasCurrentWorkObligation() || HasWorkStartingWithin(gameHours);
        }

        private bool HasWorkStartingWithinPreparationWindow()
        {
            return HasWorkStartingWithin(ObligationWakeLeadGameHours);
        }

        private bool HasWorkStartingWithin(float gameHours)
        {
            if (gameHours < 0f ||
                float.IsNaN(gameHours) ||
                float.IsInfinity(gameHours) ||
                identity == null ||
                WorkforceManager.Instance == null ||
                SimulationManager.Instance == null ||
                WorkforceManager.Instance.TryGetCurrentShift(
                    identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out _))
            {
                return false;
            }

            if (!WorkforceManager.Instance.TryGetNextShift(
                    identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out ScheduledWorkOccurrence nextShift))
            {
                return false;
            }

            return nextShift.TimeUntilStart(SimulationManager.Instance.CurrentGameHour) <= gameHours;
        }

        private bool HasCurrentWorkObligation()
        {
            return identity != null &&
                   WorkforceManager.Instance != null &&
                   SimulationManager.Instance != null &&
                   WorkforceManager.Instance.TryGetCurrentShift(
                       identity,
                       SimulationManager.Instance.CurrentGameHour,
                       out _);
        }

        private bool TryGetNextWork(out ScheduledWorkOccurrence occurrence)
        {
            occurrence = null;
            return identity != null &&
                   WorkforceManager.Instance != null &&
                   SimulationManager.Instance != null &&
                   WorkforceManager.Instance.TryGetNextShift(
                       identity,
                       SimulationManager.Instance.CurrentGameHour,
                       out occurrence);
        }

        private void RecordDecision(
            string eventKey,
            string reason,
            UnityEngine.Object target = null,
            params SimulationLogField[] fields)
        {
            string targetName = target != null ? target.name : string.Empty;
            string signature = eventKey + "|" + reason + "|" + targetName;
            if (string.Equals(signature, lastDecisionSignature, StringComparison.Ordinal))
                return;

            lastDecisionSignature = signature;
            lastDecision = eventKey;
            lastDecisionReason = reason ?? string.Empty;
            SimulationLogManager.RecordEvent(
                eventKey,
                "Decision",
                "Info",
                this,
                target,
                MergeFields(new SimulationLogField("reason", reason), fields));
        }

        private void HandleActivityLifecycleChanged(ActivityLifecycleEvent activityEvent)
        {
            if (activityEvent == null)
                return;

            string eventKey;
            switch (activityEvent.Kind)
            {
                case ActivityLifecycleEventKind.Requested:
                    eventKey = "activity.requested";
                    break;
                case ActivityLifecycleEventKind.Reserved:
                    eventKey = "activity.reserved";
                    break;
                case ActivityLifecycleEventKind.NavigationStarted:
                    eventKey = "activity.navigation_started";
                    break;
                case ActivityLifecycleEventKind.ActiveStarted:
                    eventKey = "activity.active_started";
                    break;
                case ActivityLifecycleEventKind.ExitStarted:
                    eventKey = "activity.exit_started";
                    break;
                case ActivityLifecycleEventKind.Released:
                    eventKey = "activity.released";
                    break;
                case ActivityLifecycleEventKind.Failed:
                    eventKey = "activity.failed";
                    break;
                default:
                    return;
            }

            SimulationLogManager.RecordEvent(
                eventKey,
                "Activity",
                activityEvent.Kind == ActivityLifecycleEventKind.Failed ? "Warning" : "Info",
                this,
                activityEvent.Facility,
                new SimulationLogField("activityId", activityEvent.ActivityId),
                new SimulationLogField("reservationGroup", activityEvent.ReservationGroup),
                new SimulationLogField("reason", activityEvent.Reason));
        }

        private static SimulationLogField[] MergeFields(
            SimulationLogField first,
            SimulationLogField[] remaining)
        {
            int count = remaining != null ? remaining.Length : 0;
            SimulationLogField[] fields = new SimulationLogField[count + 1];
            fields[0] = first;
            if (remaining != null)
                Array.Copy(remaining, 0, fields, 1, remaining.Length);
            return fields;
        }

        private static bool IsTargetValid(ActivityTarget target)
        {
            return target != null && target.IsConfigured;
        }

        private bool IsFoodTargetValid(ActivityTarget target)
        {
            return IsTargetValid(target) &&
                   FoodManager.Instance != null &&
                   FoodManager.Instance.IsFoodTargetLive(target);
        }

        private bool HasDependencies()
        {
            return stats != null &&
                   identity != null &&
                   targetResolver != null &&
                   activityRunner != null;
        }
    }
}
