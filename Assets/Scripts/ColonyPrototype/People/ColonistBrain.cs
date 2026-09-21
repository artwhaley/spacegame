using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum ColonistBrainState
    {
        Idle,
        SleepSeeking,
        Sleeping
    }

    [DisallowMultipleComponent]
    public sealed class ColonistBrain : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float ObligationWakeLeadGameHours = 0.5f;

        [SerializeField]
        private ColonistStatsComponent stats;

        [SerializeField]
        private ColonistTargetResolver targetResolver;

        [SerializeField]
        private ColonistActivityRunner activityRunner;

        [SerializeField]
        private ColonistBrainState state = ColonistBrainState.Idle;

        private ActivityTarget sleepTargetInProgress;
        private bool wakeRequested;

        public ColonistBrainState State => state;

        public int SimulationTickPriority => 100;

        private void Awake()
        {
            if (stats == null)
                stats = GetComponent<ColonistStatsComponent>();

            if (targetResolver == null)
                targetResolver = GetComponent<ColonistTargetResolver>();

            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();

            if (!HasDependencies())
            {
                Debug.LogError(
                    $"{name}: ColonistBrain requires ColonistStatsComponent, " +
                    "ColonistTargetResolver, and ColonistActivityRunner on the same GameObject.",
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
            }
        }

        private void TickIdle()
        {
            if (!stats.IsSleepy || activityRunner.HasActiveRequest)
                return;

            if (!targetResolver.TryResolveTarget(
                    ActivityPurpose.Sleep,
                    out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured)
            {
                return;
            }

            if (!activityRunner.RequestActivity(target.Facility, target.ActivityId))
                return;

            sleepTargetInProgress = target;
            wakeRequested = false;
            state = ColonistBrainState.SleepSeeking;
        }

        private void TickSleepSeeking()
        {
            if (sleepTargetInProgress == null ||
                !sleepTargetInProgress.IsConfigured)
            {
                FinishSleepLifecycle();
                return;
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

        private bool HasUpcomingObligationWithin(float gameHours)
        {
            // Next slice: query the colonist's obligation/employment source.
            // Wake early enough to prepare/travel for an upcoming obligation.
            return false;
        }

        private bool HasEmergencyWakeOverride()
        {
            // Future emergency/evacuation/medical orders can force waking here.
            return false;
        }

        private bool HasDependencies()
        {
            return stats != null && targetResolver != null && activityRunner != null;
        }
    }
}
