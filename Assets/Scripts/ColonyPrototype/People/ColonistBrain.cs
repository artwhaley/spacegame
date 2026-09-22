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
        private OffDutyDrive? activeOffDutyDrive;
        private readonly OffDutyCompletionHistory offDutyCompletionHistory =
            new OffDutyCompletionHistory();
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
        public OffDutyCompletionHistory OffDutyCompletionHistory =>
            offDutyCompletionHistory;
        public OffDutyDrive? ActiveOffDutyDrive => activeOffDutyDrive;
        public OffDutyDrive? PreferredOffDutyDrive => ResolvePreferredDrive();
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
                    LogSubject,
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
            bool targetValid = IsFoodTargetValid(eatTargetInProgress);
            bool accessValid = targetValid && CanRequesterStillAccessFood(eatTargetInProgress);
            if (!targetValid ||
                !accessValid ||
                (HasCurrentWorkObligation() && !stats.IsCriticallyHungry))
            {
                RequestEatStop(
                    !targetValid
                        ? "food_target_invalid"
                        : !accessValid
                            ? "food_access_lost"
                            : null);
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
            bool targetInvalid = !IsFoodTargetValid(eatTargetInProgress) ||
                                 !IsCurrentEatActivity();
            // The meal itself is already served: only structural target validity matters here.
            // Current staffing access is deliberately not re-checked, so a diner is never
            // ejected because the waiter clocked out.
            if (targetInvalid ||
                stats.Hunger <= 0f ||
                (HasCurrentWorkObligation() && !stats.IsCriticallyHungry))
            {
                RequestEatStop(targetInvalid ? "food_target_invalid" : null);
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

        private void RequestEatStop(string reason = null)
        {
            if (eatStopRequested)
                return;

            eatStopRequested = true;
            string stopReason = !string.IsNullOrEmpty(reason)
                ? reason
                : HasCurrentWorkObligation()
                    ? "work_started"
                    : stats.IsCriticallyHungry
                        ? "critical_hunger_satisfied"
                        : "hunger_satisfied";
            RecordDecision("colonist.decision.interrupted", stopReason);
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool CanRequesterStillAccessFood(ActivityTarget target)
        {
            if (FoodManager.Instance == null || target == null)
                return false;

            float currentGameHour = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentGameHour
                : 0f;
            return FoodManager.Instance.CanRequesterStillAccess(
                target,
                identity,
                currentGameHour);
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
            // OffDuty is the opportunity domain, not a need. With no unmet discretionary
            // drive there is nothing to seek, even if a recreation facility is standing next
            // to the colonist.
            OffDutyDrive? primaryDrive = ResolvePreferredDrive();
            if (!primaryDrive.HasValue)
            {
                activeOffDutyDrive = null;
                RecordDecision("colonist.decision.blocked", "discretionary_needs_satisfied");
                return;
            }

            if (OffDutyManager.Instance == null || SimulationManager.Instance == null)
            {
                RecordDecision("offduty.no_target", "manager_unavailable");
                return;
            }

            float currentGameHour = SimulationManager.Instance.CurrentGameHour;
            latestFreeTimePlan = ColonistFreeTimePlanner.Calculate(
                stats,
                identity,
                GetComponent<ColonistAssignments>(),
                currentGameHour);
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

            if (TryRequestOffDuty(primaryDrive.Value, currentGameHour, out string noTargetReason))
                return;

            // Secondary-drive fallback: only another drive that is itself above its own
            // threshold may substitute. A relaxing activity is never chosen merely because
            // "something recreational exists" while stimulation is the active drive.
            OffDutyDrive? secondaryDrive = ResolveSecondaryDrive(primaryDrive.Value);
            if (secondaryDrive.HasValue &&
                TryRequestOffDuty(secondaryDrive.Value, currentGameHour, out _))
            {
                return;
            }

            RecordDecision("offduty.no_target", noTargetReason);
        }

        private bool TryRequestOffDuty(
            OffDutyDrive drive,
            float currentGameHour,
            out string noTargetReason)
        {
            noTargetReason = "no_fitting_opportunity";
            OffDutyManager manager = OffDutyManager.Instance;
            if (manager == null)
            {
                noTargetReason = "manager_unavailable";
                return false;
            }

            OffDutyQuery query = new OffDutyQuery(
                identity,
                transform.position,
                latestFreeTimePlan.MaximumSafeDiscretionaryDuration,
                drive,
                offDutyCompletionHistory,
                currentGameHour);
            if (!manager.TryFindOpportunity(
                    query,
                    out OffDutyOpportunity opportunity,
                    out OffDutySearchReport report))
            {
                noTargetReason = report != null
                    ? report.NoTargetReason
                    : "no_fitting_opportunity";
                return false;
            }

            opportunityInProgress = opportunity;
            activeOffDutyDrive = drive;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            if (!activityRunner.RequestActivity(
                    opportunity.Target.Facility,
                    opportunity.Target.ActivityId))
            {
                opportunityInProgress = null;
                activeOffDutyDrive = null;
                noTargetReason = "request_failed";
                RecordDecision("colonist.decision.blocked", "offduty_request_failed");
                return false;
            }

            state = ColonistBrainState.OffDutySeeking;
            float need = drive == OffDutyDrive.Stimulation
                ? stats.StimulationNeed
                : stats.RelaxationNeed;
            float threshold = drive == OffDutyDrive.Stimulation
                ? stats.StimulationNeedThreshold
                : stats.RelaxationNeedThreshold;
            string driveLabel = DescribeDrive(drive);

            RecordDecision(
                "colonist.decision.offduty",
                "nearest_fitting_opportunity",
                opportunity.Target.Facility,
                new SimulationLogField("drive", driveLabel),
                new SimulationLogField("need", need),
                new SimulationLogField("threshold", threshold),
                new SimulationLogField("activityId", opportunity.Target.ActivityId),
                new SimulationLogField("cooldownKey", opportunity.Activity.CooldownKey),
                new SimulationLogField("duration", opportunity.PlannedDurationGameHours));
            SimulationLogManager.RecordEvent(
                "offduty.target_selected",
                "OffDuty",
                "Info",
                LogSubject,
                opportunity.Target.Facility,
                new SimulationLogField("drive", driveLabel),
                new SimulationLogField("need", need),
                new SimulationLogField("threshold", threshold),
                new SimulationLogField("activityId", opportunity.Target.ActivityId),
                new SimulationLogField("cooldownKey", opportunity.Activity.CooldownKey),
                new SimulationLogField("reason", "nearest_fitting_opportunity"),
                new SimulationLogField("duration", opportunity.PlannedDurationGameHours));
            return true;
        }

        private OffDutyDrive? ResolvePreferredDrive()
        {
            if (stats == null)
                return null;

            bool wantsStimulation = stats.NeedsStimulation;
            bool wantsRelaxation = stats.NeedsRelaxation;
            if (!wantsStimulation && !wantsRelaxation)
                return null;

            if (wantsStimulation && !wantsRelaxation)
                return OffDutyDrive.Stimulation;

            if (wantsRelaxation && !wantsStimulation)
                return OffDutyDrive.Relaxation;

            float stimulationPressure =
                NormalizedPressure(stats.StimulationNeed, stats.StimulationNeedThreshold);
            float relaxationPressure =
                NormalizedPressure(stats.RelaxationNeed, stats.RelaxationNeedThreshold);

            // Deterministic tie break: stimulation wins an exact tie. No randomness yet.
            return relaxationPressure > stimulationPressure
                ? OffDutyDrive.Relaxation
                : OffDutyDrive.Stimulation;
        }

        private OffDutyDrive? ResolveSecondaryDrive(OffDutyDrive primary)
        {
            OffDutyDrive other = primary == OffDutyDrive.Stimulation
                ? OffDutyDrive.Relaxation
                : OffDutyDrive.Stimulation;
            bool active = other == OffDutyDrive.Stimulation
                ? stats.NeedsStimulation
                : stats.NeedsRelaxation;
            return active ? other : (OffDutyDrive?)null;
        }

        private static float NormalizedPressure(float need, float threshold)
        {
            if (threshold <= 0f || float.IsNaN(threshold))
                return need > 0f ? float.PositiveInfinity : 0f;

            return need / threshold;
        }

        private static string DescribeDrive(OffDutyDrive drive)
        {
            return drive == OffDutyDrive.Stimulation ? "stimulation" : "relaxation";
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
            bool completed = string.Equals(
                reason,
                "planned_duration_complete",
                StringComparison.Ordinal);

            // Cooldown starts only on a genuine full-duration completion. Discovery,
            // reservation, walking, entry, becoming active and being interrupted early all
            // leave the activity immediately eligible again.
            string cooldownKey = string.Empty;
            float cooldownUntil = 0f;
            if (completed &&
                opportunityInProgress != null &&
                opportunityInProgress.Activity != null)
            {
                OffDutyActivityBinding completedActivity = opportunityInProgress.Activity;
                cooldownKey = completedActivity.CooldownKey;
                float currentGameHour = SimulationManager.Instance != null
                    ? SimulationManager.Instance.CurrentGameHour
                    : 0f;
                offDutyCompletionHistory.RecordCompletion(cooldownKey, currentGameHour);
                if (offDutyCompletionHistory.TryGetCooldownUntil(
                        cooldownKey,
                        completedActivity.CooldownGameHours,
                        out float recordedUntil))
                {
                    cooldownUntil = recordedUntil;
                }
            }

            RecordDecision(
                completed
                    ? "colonist.decision.reconsidered"
                    : "colonist.decision.interrupted",
                reason,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null);
            SimulationLogManager.RecordEvent(
                completed ? "offduty.completed" : "offduty.interrupted",
                "OffDuty",
                "Info",
                LogSubject,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null,
                new SimulationLogField("reason", reason),
                new SimulationLogField("activeDuration", actualActiveOffDutyGameHours),
                new SimulationLogField(
                    "drive",
                    activeOffDutyDrive.HasValue
                        ? DescribeDrive(activeOffDutyDrive.Value)
                        : "unknown"),
                new SimulationLogField("cooldownKey", cooldownKey),
                new SimulationLogField("cooldownUntil", cooldownUntil));
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private void FinishOffDutyLifecycle()
        {
            opportunityInProgress = null;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            activeOffDutyDrive = null;
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

        /// <summary>
        /// Canonical structured-log subject for this colonist. Game-side colonist events are
        /// attributed to the ColonistIdentity instead of to whichever sibling component emitted
        /// them, so querying "this colonist's history" cannot silently miss needs, decisions or
        /// activity lifecycle entries recorded by another of the colonist's components.
        /// </summary>
        private UnityEngine.Object LogSubject
        {
            get
            {
                if (identity == null)
                    identity = GetComponent<ColonistIdentity>();

                return identity != null ? (UnityEngine.Object)identity : (UnityEngine.Object)this;
            }
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
                LogSubject,
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
                LogSubject,
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
