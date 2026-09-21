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
        Eating
    }

    [DisallowMultipleComponent]
    public sealed class ColonistBrain : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float ObligationWakeLeadGameHours = 0.5f;

        [SerializeField]
        private ColonistStatsComponent stats;

        [SerializeField]
        private ColonistIdentity identity;

        [SerializeField]
        private ColonistTargetResolver targetResolver;

        [SerializeField]
        private ColonistActivityRunner activityRunner;

        [SerializeField]
        private ColonistBrainState state = ColonistBrainState.Idle;

        private ActivityTarget sleepTargetInProgress;
        private ActivityTarget workTargetInProgress;
        private ActivityTarget eatTargetInProgress;
        private bool wakeRequested;
        private bool workStopRequested;
        private bool eatStopRequested;

        public ColonistBrainState State => state;

        public int SimulationTickPriority => 100;

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
            }
        }

        private void TickIdle()
        {
            if (activityRunner.HasActiveRequest)
                return;

            if (HasCurrentWorkObligation())
            {
                TryStartWork();
                return;
            }

            if (HasUpcomingObligationWithin(ObligationWakeLeadGameHours))
                return;

            if (stats.IsSleepy)
            {
                TryStartSleep();
                return;
            }

            if (stats.IsHungry)
                TryStartEat();
        }

        private void TickSleepSeeking()
        {
            if (sleepTargetInProgress == null ||
                !sleepTargetInProgress.IsConfigured)
            {
                FinishSleepLifecycle();
                return;
            }

            if (!wakeRequested &&
                HasUpcomingObligationWithin(ObligationWakeLeadGameHours))
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
            if (sleepTargetInProgress == null ||
                !sleepTargetInProgress.IsConfigured)
            {
                FinishSleepLifecycle();
                return;
            }

            if (wakeRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishSleepLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
            {
                FinishSleepLifecycle();
                return;
            }

            if (!activityRunner.IsActivityActive ||
                !string.Equals(
                    activityRunner.ActiveActivityId,
                    sleepTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (stats.Fatigue <= 0f)
            {
                RequestWake();
                return;
            }

            if (HasUpcomingObligationWithin(ObligationWakeLeadGameHours))
            {
                RequestWake();
                return;
            }

            if (HasEmergencyWakeOverride())
                RequestWake();
        }

        private void RequestWake()
        {
            wakeRequested = true;
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
            if (!targetResolver.TryResolveTarget(
                    ActivityPurpose.Sleep,
                    out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured)
            {
                return false;
            }

            if (!activityRunner.RequestActivity(target.Facility, target.ActivityId))
                return false;

            sleepTargetInProgress = target;
            wakeRequested = false;
            state = ColonistBrainState.SleepSeeking;
            return true;
        }

        private void TickWorkSeeking()
        {
            if (workTargetInProgress == null ||
                !workTargetInProgress.IsConfigured)
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (!HasCurrentWorkObligation())
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
            if (workTargetInProgress == null ||
                !workTargetInProgress.IsConfigured)
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (!workStopRequested &&
                (!HasCurrentWorkObligation() || !IsCurrentWorkActivity()))
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
            {
                FinishWorkLifecycle();
            }
        }

        private bool TryStartWork()
        {
            if (!targetResolver.TryResolveTarget(
                    ActivityPurpose.Work,
                    out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured)
            {
                return false;
            }

            if (!activityRunner.RequestActivity(target.Facility, target.ActivityId))
                return false;

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
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool IsCurrentWorkActivity()
        {
            return activityRunner.HasActiveRequest &&
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
            if (eatTargetInProgress == null ||
                !eatTargetInProgress.IsConfigured ||
                FoodManager.Instance == null ||
                !FoodManager.Instance.IsFoodTargetLive(eatTargetInProgress))
            {
                RequestEatStop();
                if (!activityRunner.HasActiveRequest)
                    FinishEatLifecycle();
                return;
            }

            if (HasUpcomingObligationWithin(ObligationWakeLeadGameHours))
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
            if (eatTargetInProgress == null ||
                !eatTargetInProgress.IsConfigured ||
                FoodManager.Instance == null ||
                !FoodManager.Instance.IsFoodTargetLive(eatTargetInProgress) ||
                !IsCurrentEatActivity())
            {
                RequestEatStop();
            }
            else if (HasUpcomingObligationWithin(ObligationWakeLeadGameHours) ||
                     stats.Hunger <= 0f)
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
                HasUpcomingObligationWithin(ObligationWakeLeadGameHours) ||
                !targetResolver.TryResolveTarget(
                    ActivityPurpose.Eat,
                    out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured ||
                FoodManager.Instance == null ||
                !FoodManager.Instance.IsFoodTargetLive(target))
            {
                return false;
            }

            if (!activityRunner.RequestActivity(target.Facility, target.ActivityId))
                return false;

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
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool IsCurrentEatActivity()
        {
            return activityRunner.HasActiveRequest &&
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

        private bool HasUpcomingObligationWithin(float gameHours)
        {
            if (gameHours < 0f ||
                float.IsNaN(gameHours) ||
                float.IsInfinity(gameHours) ||
                identity == null ||
                WorkforceManager.Instance == null ||
                SimulationManager.Instance == null)
            {
                return false;
            }

            float currentGameHour = SimulationManager.Instance.CurrentGameHour;
            if (WorkforceManager.Instance.TryGetCurrentShift(
                    identity,
                    currentGameHour,
                    out _))
            {
                return true;
            }

            if (!WorkforceManager.Instance.TryGetNextShift(
                    identity,
                    currentGameHour,
                    out ScheduledWorkOccurrence nextShift))
            {
                return false;
            }

            return nextShift.TimeUntilStart(currentGameHour) <= gameHours;
        }

        private bool HasCurrentWorkObligation()
        {
            if (identity == null ||
                WorkforceManager.Instance == null ||
                SimulationManager.Instance == null)
            {
                return false;
            }

            return WorkforceManager.Instance.TryGetCurrentShift(
                identity,
                SimulationManager.Instance.CurrentGameHour,
                out _);
        }

        private bool HasEmergencyWakeOverride()
        {
            // Future emergency/evacuation/medical orders can force waking here.
            return false;
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
