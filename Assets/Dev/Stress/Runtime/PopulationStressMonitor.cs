using System;
using System.Collections.Generic;
using AsteroidColony;
using Colony.Interactions;
using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] private string[] requiredActivityIds = { "Work", "Eat", "Dance", "Sleep" };

        private ActorObservation[] observations = Array.Empty<ActorObservation>();
        private RunnerHook[] runnerHooks = Array.Empty<RunnerHook>();
        private StatusHook[] statusHooks = Array.Empty<StatusHook>();
        private AnimationHook[] animationHooks = Array.Empty<AnimationHook>();
        private double sampleTimer;
        private long lastTick;
        private long lastInvariantTick = -1L;
        private long lastFoodQueries;
        private long lastFoodCandidates;
        private long lastFoodSelections;
        private long lastOffDutyQueries;
        private long lastOffDutyCandidates;
        private int lastReleasedActorId;
        private int lastReleasedFacilityId;
        private long lastReleasedTick = -1L;
        private string lastReleasedActivityId = string.Empty;
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
            lastInvariantTick = -1L;
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
            CheckInvariants(true);
            CheckCoverage();
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

        private void CheckFacilityOwnership()
        {
            long tick = simulationManager != null ? simulationManager.CurrentTick : 0L;
            double seconds = simulationManager != null
                ? simulationManager.CurrentSimulationSeconds
                : 0d;

            for (int facilityIndex = 0; facilityIndex < facilities.Length; facilityIndex++)
            {
                InteractableFacility facility = facilities[facilityIndex];
                if (facility == null || facility.Activities == null)
                    continue;

                IReadOnlyList<FacilityActivityBinding> bindings = facility.Activities;
                for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    FacilityActivityBinding binding = bindings[bindingIndex];
                    if (binding == null || string.IsNullOrEmpty(binding.ReservationGroup))
                        continue;

                    bool groupAlreadyChecked = false;
                    for (int priorIndex = 0; priorIndex < bindingIndex; priorIndex++)
                    {
                        FacilityActivityBinding prior = bindings[priorIndex];
                        if (prior != null && string.Equals(
                                prior.ReservationGroup,
                                binding.ReservationGroup,
                                StringComparison.Ordinal))
                        {
                            groupAlreadyChecked = true;
                            break;
                        }
                    }

                    if (groupAlreadyChecked)
                        continue;

                    if (!facility.TryGetReservation(binding.ReservationGroup, out FacilityReservationToken token))
                        continue;

                    if (token.Generation <= 0)
                    {
                        telemetry.RecordOwnershipDiagnostic(
                            StressMetric.InvalidReservationGenerations,
                            0,
                            StableFacilityId(facility),
                            token.ReservationGroup,
                            token.Generation,
                            "facility reservation generation is not positive",
                            tick,
                            seconds);
                    }

                    ColonistActivityRunner owner = token.Owner as ColonistActivityRunner;
                    if (owner == null)
                    {
                        telemetry.RecordOwnershipDiagnostic(
                            StressMetric.OrphanedFacilityReservations,
                            0,
                            StableFacilityId(facility),
                            token.ReservationGroup,
                            token.Generation,
                            "facility reservation owner is not a live colonist runner",
                            tick,
                            seconds);
                        continue;
                    }

                    FacilityReservationToken runnerToken = owner.CurrentReservation;
                    if (runnerToken == null ||
                        !ReferenceEquals(runnerToken, token) ||
                        owner.CurrentFacility != facility)
                    {
                        telemetry.RecordOwnershipDiagnostic(
                            StressMetric.RunnerReservationOwnershipMismatches,
                            StableActorId(owner.gameObject, 0),
                            StableFacilityId(facility),
                            token.ReservationGroup,
                            token.Generation,
                            "facility token is not the runner's current token",
                            tick,
                            seconds);
                    }
                }
            }

            for (int observationIndex = 0; observationIndex < observations.Length; observationIndex++)
            {
                ActorObservation observation = observations[observationIndex];
                ColonistActivityRunner runner = observation.Runner;
                FacilityReservationToken token = runner != null ? runner.CurrentReservation : null;
                if (runner == null || token == null || runner.CurrentFacility == null)
                    continue;

                if (!runner.CurrentFacility.TryGetReservation(
                        token.ReservationGroup,
                        out FacilityReservationToken facilityToken) ||
                    !ReferenceEquals(facilityToken, token))
                {
                    telemetry.RecordOwnershipDiagnostic(
                        StressMetric.RunnerReservationOwnershipMismatches,
                        observation.ActorId,
                        StableFacilityId(runner.CurrentFacility),
                        token.ReservationGroup,
                        token.Generation,
                        "runner token is missing or differs from the facility token",
                        tick,
                        seconds);
                }
            }
        }

        private void CheckCoverage()
        {
            if (!observing || telemetry == null || requiredActivityIds == null)
                return;

            long tick = simulationManager != null ? simulationManager.CurrentTick : 0L;
            double seconds = simulationManager != null
                ? simulationManager.CurrentSimulationSeconds
                : 0d;
            for (int index = 0; index < requiredActivityIds.Length; index++)
            {
                string activityId = requiredActivityIds[index];
                if (string.IsNullOrEmpty(activityId) ||
                    !telemetry.TryGetActivityDiagnostic(
                        activityId,
                        out StressActivityDiagnostic diagnostic) ||
                    diagnostic.ActiveStarted > 0L)
                {
                    continue;
                }

                telemetry.RecordCoverageDiagnostic(
                    activityId,
                    "required activity produced no active lifecycle start",
                    tick,
                    seconds);
            }
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
                    Brain = actor != null ? actor.GetComponent<ColonistBrain>() : null,
                    Identity = actor != null ? actor.GetComponent<StressStableIdentity>() : null,
                    ActorId = StableActorId(actor, i),
                    LastPhase = ActivityPhase.Idle,
                    LastActivityId = string.Empty,
                    PhaseStartTick = 0L
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
            statusHooks = new StatusHook[observations.Length];
            animationHooks = new AnimationHook[observations.Length];
            for (int i = 0; i < observations.Length; i++)
            {
                ActorObservation observation = observations[i];
                if (observation.Runner != null)
                {
                    RunnerHook hook = new RunnerHook(this, observation.Runner, observation.ActorId);
                    runnerHooks[i] = hook;
                    observation.Runner.ActivityLifecycleChanged += hook.Handle;
                    StatusHook statusHook = new StatusHook(this, observation.Runner, observation.ActorId);
                    statusHooks[i] = statusHook;
                    observation.Runner.StatusChanged += statusHook.Handle;
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
                if (statusHooks[i] != null && observations[i].Runner != null)
                    observations[i].Runner.StatusChanged -= statusHooks[i].Handle;
                if (animationHooks[i] != null && observations[i].Animation != null)
                    observations[i].Animation.FailureOccurred -= animationHooks[i].Handle;
            }

            runnerHooks = Array.Empty<RunnerHook>();
            statusHooks = Array.Empty<StatusHook>();
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
                case ActivityLifecycleEventKind.Failed:
                    kind = StressEventKind.ActivityFailed;
                    break;
                default:
                    kind = StressEventKind.ActivityFailed;
                    break;
            }

            FacilityReservationToken token = runner != null ? runner.CurrentReservation : null;
            ColonistBrain brain = runner != null ? runner.GetComponent<ColonistBrain>() : null;
            long tick = simulationManager != null ? simulationManager.CurrentTick : 0L;
            double seconds = simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d;

            if (kind == StressEventKind.ReservationReleased)
            {
                int facilityId = StableFacilityId(lifecycle.Facility);
                if (lastReleasedTick == tick &&
                    lastReleasedActorId == actorId &&
                    lastReleasedFacilityId == facilityId &&
                    string.Equals(lastReleasedActivityId, lifecycle.ActivityId, StringComparison.Ordinal))
                {
                    telemetry.RecordOwnershipDiagnostic(
                        StressMetric.DuplicateReservationReleases,
                        actorId,
                        facilityId,
                        lifecycle.ReservationGroup,
                        token != null ? token.Generation : 0,
                        "duplicate reservation release lifecycle event",
                        tick,
                        seconds);
                }

                lastReleasedTick = tick;
                lastReleasedActorId = actorId;
                lastReleasedFacilityId = facilityId;
                lastReleasedActivityId = lifecycle.ActivityId ?? string.Empty;
            }

            telemetry.RecordActivityEvent(
                kind,
                actorId,
                StableFacilityId(lifecycle.Facility),
                lifecycle.ActivityId,
                lifecycle.Reason,
                tick,
                seconds,
                token != null ? token.Generation : 0,
                runner != null ? runner.Phase.ToString() : string.Empty,
                brain != null ? brain.State.ToString() : string.Empty,
                runner != null && runner.IsExitInProgress);
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

        private void HandleStatusChanged(
            ColonistActivityRunner runner,
            int actorId,
            string status)
        {
            if (!observing || telemetry == null || runner == null || string.IsNullOrEmpty(status))
                return;

            if (status.StartsWith("Exited ", StringComparison.Ordinal))
            {
                int start = "Exited ".Length;
                int end = status.IndexOf(" and ", start, StringComparison.Ordinal);
                if (end < 0)
                    end = status.IndexOf(", cleared", start, StringComparison.Ordinal);
                if (end > start)
                {
                    ColonistBrain completedBrain = runner.GetComponent<ColonistBrain>();
                    telemetry.RecordActivityEvent(
                        StressEventKind.ActivityCompleted,
                        actorId,
                        StableFacilityId(runner.CurrentFacility),
                        status.Substring(start, end - start),
                        status,
                        simulationManager != null ? simulationManager.CurrentTick : 0L,
                        simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d,
                        0,
                        runner.Phase.ToString(),
                        completedBrain != null ? completedBrain.State.ToString() : string.Empty);
                }

                return;
            }

            bool denied = status.StartsWith("Busy: reservation group", StringComparison.Ordinal) ||
                          status.StartsWith("Request rejected:", StringComparison.Ordinal);
            if (!denied)
                return;

            ColonistBrain brain = runner.GetComponent<ColonistBrain>();
            FacilityReservationToken token = runner.CurrentReservation;
            telemetry.RecordActivityDenied(
                actorId,
                StableFacilityId(runner.CurrentFacility),
                runner.PendingActivityId ?? runner.CurrentActivityId ?? string.Empty,
                status,
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d,
                token != null ? token.Generation : 0,
                runner.Phase.ToString(),
                brain != null ? brain.State.ToString() : string.Empty);
        }

        private void CheckInvariants(bool force = false)
        {
            if (!observing || telemetry == null)
                return;

            long tick = simulationManager != null ? simulationManager.CurrentTick : 0L;
            if (!force && tick == lastInvariantTick && lastInvariantTick >= 0L)
                return;
            lastInvariantTick = tick;

            for (int i = 0; i < observations.Length; i++)
            {
                ActorObservation observation = observations[i];
                ColonistActivityRunner runner = observation.Runner;
                if (runner == null)
                    continue;

                ActivityPhase currentPhase = runner.Phase;
                string currentActivity = runner.CurrentActivityId ?? string.Empty;
                if (observation.LastPhase != currentPhase ||
                    !string.Equals(observation.LastActivityId, currentActivity, StringComparison.Ordinal))
                {
                    observation.LastPhase = currentPhase;
                    observation.LastActivityId = currentActivity;
                    observation.PhaseStartTick = tick;
                }
                else if (currentPhase == ActivityPhase.Navigating &&
                         tick - observation.PhaseStartTick >= 600L)
                {
                    RecordInvariant(
                        observation,
                        runner.CurrentFacility,
                        StressMetric.ActivityStalled,
                        "activity has remained in navigation for at least 600 committed ticks");
                }
                observations[i] = observation;

                NavMeshAgent agent = runner.GetComponent<NavMeshAgent>();
                if (runner.Phase == ActivityPhase.Navigating &&
                    (agent == null ||
                     !agent.isOnNavMesh ||
                     (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)))
                {
                    RecordInvariant(
                        observation,
                        runner.CurrentFacility,
                        StressMetric.InvalidNavigationState,
                        "navigation phase has no complete usable NavMesh path");
                }

                if (observation.Stats != null &&
                    (!IsFinite(observation.Stats.Hunger) ||
                     !IsFinite(observation.Stats.Fatigue)))
                {
                    RecordInvariant(
                        observation,
                        runner.CurrentFacility,
                        StressMetric.InvalidNumericState,
                        "need value is NaN or Infinity");
                }

                if (!IsFinite(runner.transform.position))
                {
                    RecordInvariant(
                        observation,
                        runner.CurrentFacility,
                        StressMetric.InvalidNumericState,
                        "actor position is NaN or Infinity");
                }

                if (runner.IsActivityActive && runner.CurrentReservation == null)
                {
                    RecordInvariant(
                        observation,
                        runner.ActiveFacility,
                        StressMetric.ActiveWithoutReservation,
                        "active activity has no reservation");
                }

                if (runner.CurrentReservation != null && runner.CurrentFacility == null)
                {
                    RecordInvariant(
                        observation,
                        null,
                        StressMetric.OrphanedReservations,
                        "reservation remains without a facility");
                }

                if (observation.Stats != null &&
                    observation.Stats.IsCriticallyHungry &&
                    runner.IsActivityActive)
                {
                    if (string.Equals(runner.ActiveActivityId, "Eat", StringComparison.Ordinal))
                    {
                        telemetry.Increment(StressMetric.CriticalHungerEatAllowed);
                    }
                    else
                    {
                        RecordInvariant(
                            observation,
                            runner.ActiveFacility,
                            StressMetric.CriticalHungerIllegalActivity,
                            "critically hungry actor is performing " +
                            (runner.ActiveActivityId ?? "an unknown activity"));
                    }
                }
            }

            CheckFacilityOwnership();
        }

        private void RecordInvariant(
            ActorObservation observation,
            InteractableFacility facility,
            StressMetric metric,
            string detail)
        {
            telemetry.Increment(StressMetric.InvariantViolations);
            telemetry.Increment(metric);
            ColonistActivityRunner runner = observation.Runner;
            FacilityReservationToken token = runner != null ? runner.CurrentReservation : null;
            ColonistBrain brain = observation.Brain;
            string evidence = detail +
                "; activity=" + (runner != null ? runner.CurrentActivityId ?? string.Empty : string.Empty) +
                "; phase=" + (runner != null ? runner.Phase.ToString() : string.Empty) +
                "; stopRequested=" + (runner != null && runner.IsExitInProgress) +
                "; generation=" + (token != null ? token.Generation.ToString() : "0") +
                "; hunger=" + (observation.Stats != null ? observation.Stats.Hunger.ToString("R") : "n/a") +
                "; brain=" + (brain != null ? brain.State.ToString() : string.Empty);
            telemetry.RecordActivityEvent(
                StressEventKind.InvariantViolation,
                observation.ActorId,
                StableFacilityId(facility),
                runner != null ? runner.CurrentActivityId : string.Empty,
                evidence,
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d,
                token != null ? token.Generation : 0,
                runner != null ? runner.Phase.ToString() : string.Empty,
                brain != null ? brain.State.ToString() : string.Empty,
                runner != null && runner.IsExitInProgress);
            telemetry.RecordFailure(
                StressEventKind.InvariantViolation,
                observation.ActorId,
                StableFacilityId(facility),
                evidence,
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                runner != null ? runner.Phase.ToString() : string.Empty,
                brain != null ? brain.State.ToString() : string.Empty,
                token != null ? token.Generation : 0);
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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
            public ColonistBrain Brain;
            public StressStableIdentity Identity;
            public int ActorId;
            public ActivityPhase LastPhase;
            public string LastActivityId;
            public long PhaseStartTick;
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

        private sealed class StatusHook
        {
            private readonly PopulationStressMonitor monitor;
            private readonly ColonistActivityRunner runner;
            private readonly int actorId;

            public StatusHook(PopulationStressMonitor owner, ColonistActivityRunner source, int id)
            {
                monitor = owner;
                runner = source;
                actorId = id;
            }

            public void Handle(string status)
            {
                monitor.HandleStatusChanged(runner, actorId, status);
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
