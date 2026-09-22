using System;
using System.Collections.Generic;
using AsteroidColony;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony.Stress
{
    /// <summary>
    /// Low-frequency population observer. It subscribes to existing lifecycle
    /// events and samples invariants once per real second, keeping hot-path
    /// instrumentation out of the simulation tick loop.
    /// </summary>
    public sealed class PopulationStressMonitor : MonoBehaviour
    {
        [SerializeField] private SimulationManager simulationManager;
        [SerializeField] private StressTelemetry telemetry;
        [SerializeField] private bool autoDiscoverActors = true;
        [SerializeField, Min(0.1f)] private float invariantSampleIntervalSeconds = 1f;
        [SerializeField] private ColonistActivityRunner[] actors = Array.Empty<ColonistActivityRunner>();
        [SerializeField] private InteractableFacility[] facilities = Array.Empty<InteractableFacility>();

        private ActorObservation[] observations = Array.Empty<ActorObservation>();
        private RunnerHook[] runnerHooks = Array.Empty<RunnerHook>();
        private AnimationHook[] animationHooks = Array.Empty<AnimationHook>();
        private double sampleTimer;
        private long lastTick;
        private long lastFoodQueries;
        private long lastFoodCandidates;
        private long lastFoodSelections;
        private long lastOffDutyQueries;
        private long lastOffDutyCandidates;
        private bool observing;

        public IReadOnlyList<ColonistActivityRunner> Actors => actors;
        public IReadOnlyList<InteractableFacility> Facilities => facilities;
        public bool IsObserving => observing;

        private void Awake()
        {
            if (simulationManager == null)
                simulationManager = SimulationManager.Instance;
            if (telemetry == null)
                telemetry = StressTelemetry.Instance;
            if (autoDiscoverActors && actors.Length == 0)
                DiscoverActors();
            PrepareObservations();
        }

        private void OnEnable()
        {
            if (observations.Length == 0)
                PrepareObservations();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!observing || telemetry == null)
                return;

            telemetry.RecordFrame(Time.unscaledDeltaTime);
            if (simulationManager == null)
                simulationManager = SimulationManager.Instance;

            if (simulationManager != null)
            {
                long currentTick = simulationManager.CurrentTick;
                if (currentTick > lastTick)
                    telemetry.Increment(StressMetric.SimulationTicks, currentTick - lastTick);
                lastTick = currentTick;
                telemetry.SetSimulationProgress(
                    simulationManager.CurrentTick,
                    simulationManager.CurrentSimulationSeconds);
            }

            sampleTimer += Time.unscaledDeltaTime;
            if (sampleTimer < invariantSampleIntervalSeconds)
                return;

            sampleTimer = 0d;
            RecordManagerDiagnostics();
            CheckInvariants();
        }

        public void Configure(
            SimulationManager manager,
            StressTelemetry stressTelemetry,
            ColonistActivityRunner[] configuredActors,
            InteractableFacility[] configuredFacilities)
        {
            simulationManager = manager;
            telemetry = stressTelemetry;
            actors = configuredActors ?? Array.Empty<ColonistActivityRunner>();
            facilities = configuredFacilities ?? Array.Empty<InteractableFacility>();
            PrepareObservations();
        }

        public void BeginObservation()
        {
            if (telemetry == null)
                telemetry = StressTelemetry.Instance;
            if (simulationManager == null)
                simulationManager = SimulationManager.Instance;
            if (telemetry == null)
                return;

            observing = true;
            sampleTimer = 0d;
            lastTick = simulationManager != null ? simulationManager.CurrentTick : 0L;
            lastFoodQueries = FoodManager.Instance != null ? FoodManager.Instance.FoodQueryCount : 0L;
            lastFoodCandidates = FoodManager.Instance != null ? FoodManager.Instance.FoodCandidateEvaluationCount : 0L;
            lastFoodSelections = FoodManager.Instance != null ? FoodManager.Instance.FoodSelectionCount : 0L;
            lastOffDutyQueries = OffDutyManager.Instance != null ? OffDutyManager.Instance.OpportunityQueryCount : 0L;
            lastOffDutyCandidates = OffDutyManager.Instance != null ? OffDutyManager.Instance.OpportunityCandidateEvaluationCount : 0L;
            Subscribe();
        }

        public void EndObservation()
        {
            if (!observing)
                return;
            RecordManagerDiagnostics();
            CheckInvariants();
            observing = false;
        }

        private void RecordManagerDiagnostics()
        {
            FoodManager food = FoodManager.Instance;
            if (food != null)
            {
                RecordCounterDelta(StressMetric.FoodQueries, food.FoodQueryCount, ref lastFoodQueries);
                RecordCounterDelta(StressMetric.FoodCandidateEvaluations, food.FoodCandidateEvaluationCount, ref lastFoodCandidates);
                RecordCounterDelta(StressMetric.FoodSelections, food.FoodSelectionCount, ref lastFoodSelections);
            }

            OffDutyManager offDuty = OffDutyManager.Instance;
            if (offDuty != null)
            {
                RecordCounterDelta(StressMetric.OffDutyQueries, offDuty.OpportunityQueryCount, ref lastOffDutyQueries);
                RecordCounterDelta(StressMetric.OffDutyCandidateEvaluations, offDuty.OpportunityCandidateEvaluationCount, ref lastOffDutyCandidates);
            }
        }

        private void RecordCounterDelta(StressMetric metric, long current, ref long previous)
        {
            if (current > previous)
                telemetry.Increment(metric, current - previous);
            previous = current;
        }

        private void DiscoverActors()
        {
            actors = FindObjectsByType<ColonistActivityRunner>(FindObjectsInactive.Exclude);
            Array.Sort(actors, CompareActors);
            facilities = FindObjectsByType<InteractableFacility>(FindObjectsInactive.Exclude);
            Array.Sort(facilities, CompareFacilities);
        }

        private void PrepareObservations()
        {
            Unsubscribe();
            observations = new ActorObservation[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            {
                ColonistActivityRunner runner = actors[i];
                GameObject actor = runner != null ? runner.gameObject : null;
                observations[i] = new ActorObservation
                {
                    Runner = runner,
                    Animation = actor != null ? actor.GetComponent<ColonistAnimationDriver>() : null,
                    Stats = actor != null ? actor.GetComponent<ColonistStatsComponent>() : null,
                    Identity = actor != null ? actor.GetComponent<StressStableIdentity>() : null,
                    ActorId = StableActorId(actor, i)
                };
            }

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Subscribe()
        {
            if (runnerHooks.Length > 0 || animationHooks.Length > 0)
                return;

            runnerHooks = new RunnerHook[observations.Length];
            animationHooks = new AnimationHook[observations.Length];
            for (int i = 0; i < observations.Length; i++)
            {
                ActorObservation observation = observations[i];
                if (observation.Runner != null)
                {
                    RunnerHook hook = new RunnerHook(this, observation.Runner, observation.ActorId);
                    runnerHooks[i] = hook;
                    observation.Runner.ActivityLifecycleChanged += hook.Handle;
                }

                if (observation.Animation != null)
                {
                    AnimationHook hook = new AnimationHook(this, observation.ActorId);
                    animationHooks[i] = hook;
                    observation.Animation.FailureOccurred += hook.Handle;
                }
            }
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < runnerHooks.Length && i < observations.Length; i++)
            {
                if (runnerHooks[i] != null && observations[i].Runner != null)
                    observations[i].Runner.ActivityLifecycleChanged -= runnerHooks[i].Handle;
                if (animationHooks[i] != null && observations[i].Animation != null)
                    observations[i].Animation.FailureOccurred -= animationHooks[i].Handle;
            }

            runnerHooks = Array.Empty<RunnerHook>();
            animationHooks = Array.Empty<AnimationHook>();
        }

        private void HandleLifecycle(
            ColonistActivityRunner runner,
            int actorId,
            ActivityLifecycleEvent lifecycle)
        {
            if (!observing || telemetry == null || lifecycle == null)
                return;

            StressEventKind kind;
            switch (lifecycle.Kind)
            {
                case ActivityLifecycleEventKind.Requested:
                    kind = StressEventKind.ActivityRequested;
                    break;
                case ActivityLifecycleEventKind.Reserved:
                    kind = StressEventKind.ActivityReserved;
                    break;
                case ActivityLifecycleEventKind.NavigationStarted:
                    kind = StressEventKind.NavigationStarted;
                    break;
                case ActivityLifecycleEventKind.ActiveStarted:
                    kind = StressEventKind.ActivityStarted;
                    break;
                case ActivityLifecycleEventKind.ExitStarted:
                    kind = StressEventKind.ExitStarted;
                    break;
                case ActivityLifecycleEventKind.Released:
                    kind = StressEventKind.ReservationReleased;
                    break;
                default:
                    kind = StressEventKind.ActivityFailed;
                    break;
            }

            telemetry.RecordActivityEvent(
                kind,
                actorId,
                StableFacilityId(lifecycle.Facility),
                lifecycle.ActivityId,
                lifecycle.Reason,
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d);
        }

        private void HandleAnimationFailure(int actorId, string reason)
        {
            if (!observing || telemetry == null)
                return;

            telemetry.RecordFailure(
                StressEventKind.AnimationFailed,
                actorId,
                0,
                reason,
                simulationManager != null ? simulationManager.CurrentTick : 0L);
        }

        private void CheckInvariants()
        {
            if (!observing || telemetry == null)
                return;

            for (int i = 0; i < observations.Length; i++)
            {
                ActorObservation observation = observations[i];
                ColonistActivityRunner runner = observation.Runner;
                if (runner == null)
                    continue;

                if (runner.IsActivityActive && runner.CurrentReservation == null)
                {
                    RecordInvariant(
                        observation.ActorId,
                        runner.ActiveFacility,
                        StressMetric.ActiveWithoutReservation,
                        "active activity has no reservation");
                }

                if (runner.CurrentReservation != null && runner.CurrentFacility == null)
                {
                    RecordInvariant(
                        observation.ActorId,
                        null,
                        StressMetric.OrphanedReservations,
                        "reservation remains without a facility");
                }

                if (observation.Stats != null &&
                    observation.Stats.IsCriticallyHungry &&
                    runner.IsActivityActive)
                {
                    RecordInvariant(
                        observation.ActorId,
                        runner.ActiveFacility,
                        StressMetric.CriticalHungerNormalActivity,
                        "critically hungry actor is performing a normal activity");
                }
            }
        }

        private void RecordInvariant(
            int actorId,
            InteractableFacility facility,
            StressMetric metric,
            string detail)
        {
            telemetry.Increment(StressMetric.InvariantViolations);
            telemetry.Increment(metric);
            telemetry.RecordActivityEvent(
                StressEventKind.InvariantViolation,
                actorId,
                StableFacilityId(facility),
                string.Empty,
                detail,
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d);
        }

        private static int StableActorId(GameObject actor, int fallbackIndex)
        {
            if (actor == null)
                return fallbackIndex + 1;
            StressStableIdentity identity = actor.GetComponent<StressStableIdentity>();
            return identity != null ? identity.StableId : StressStableIdentity.StableHash(actor.name);
        }

        private static int StableFacilityId(InteractableFacility facility)
        {
            if (facility == null)
                return 0;
            StressStableIdentity identity = facility.GetComponent<StressStableIdentity>();
            return identity != null ? identity.StableId : StressStableIdentity.StableHash(facility.name);
        }

        private static int CompareActors(ColonistActivityRunner left, ColonistActivityRunner right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }

        private static int CompareFacilities(InteractableFacility left, InteractableFacility right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }

        private struct ActorObservation
        {
            public ColonistActivityRunner Runner;
            public ColonistAnimationDriver Animation;
            public ColonistStatsComponent Stats;
            public StressStableIdentity Identity;
            public int ActorId;
        }

        private sealed class RunnerHook
        {
            private readonly PopulationStressMonitor monitor;
            private readonly ColonistActivityRunner runner;
            private readonly int actorId;

            public RunnerHook(PopulationStressMonitor owner, ColonistActivityRunner source, int id)
            {
                monitor = owner;
                runner = source;
                actorId = id;
            }

            public void Handle(ActivityLifecycleEvent lifecycle)
            {
                monitor.HandleLifecycle(runner, actorId, lifecycle);
            }
        }

        private sealed class AnimationHook
        {
            private readonly PopulationStressMonitor monitor;
            private readonly int actorId;

            public AnimationHook(PopulationStressMonitor owner, int id)
            {
                monitor = owner;
                actorId = id;
            }

            public void Handle(string reason)
            {
                monitor.HandleAnimationFailure(actorId, reason);
            }
        }
    }
}
