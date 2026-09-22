using System;
using System.Globalization;
using UnityEngine;

namespace AsteroidColony.Stress
{
    [Serializable]
    public struct StressEventRecord
    {
        public long Sequence;
        public long SimulationTick;
        public double SimulationSeconds;
        public double AbsoluteSimulationSeconds;
        public int ActorId;
        public int FacilityId;
        public int ReservationGeneration;
        public StressEventKind Kind;
        public string ActivityId;
        public string Detail;
        public string Phase;
        public string BrainState;
        public bool StopRequested;
    }

    [Serializable]
    public struct StressFailureRecord
    {
        public long Sequence;
        public long SimulationTick;
        public double SimulationSeconds;
        public int ActorId;
        public int FacilityId;
        public StressEventKind Kind;
        public string Detail;
    }

    [Serializable]
    public struct StressActivityDiagnostic
    {
        public string ActivityId;
        public long Requested;
        public long Denied;
        public long Reserved;
        public long NavigationStarted;
        public long ActiveStarted;
        public long ExitStarted;
        public long Released;
        public long Completed;
        public long Failed;
        public int UniqueActorsServed;
        public long WaitSamples;
        public long TotalWaitTicks;
        public long MaximumWaitTicks;
        public long FirstTick;
        public long LastTick;
    }

    [Serializable]
    public struct StressOwnershipDiagnostic
    {
        public long Sequence;
        public long SimulationTick;
        public int ActorId;
        public int FacilityId;
        public int ReservationGeneration;
        public string ReservationGroup;
        public string Detail;
    }

    [Serializable]
    public sealed class StressTelemetrySnapshot
    {
        public string RunId;
        public StressObservationMode Mode;
        public long[] Counters;
        public StressEventRecord[] Events;
        public StressFailureRecord[] Failures;
        public long EventCount;
        public long FailureCount;
        public long DroppedEvents;
        public long DroppedFailures;
        public StressActivityDiagnostic[] ActivityDiagnostics;
        public StressOwnershipDiagnostic[] OwnershipDiagnostics;
        public ulong Digest;
        public long StartSimulationTick;
        public long EndSimulationTick;
        public double StartSimulationSeconds;
        public double EndSimulationSeconds;
        public double ElapsedSimulationSeconds;
        public double StartRealtimeSeconds;
        public double EndRealtimeSeconds;
        public double ElapsedRealtimeSeconds;
        public double RealSeconds;
        public double SimulationSeconds;
        public long SimulationTicks;
        public long RenderedFrames;
        public double MinimumFrameSeconds;
        public double MaximumFrameSeconds;
        public double AverageFrameSeconds;
        public double P50FrameSeconds;
        public double P95FrameSeconds;
        public double P99FrameSeconds;
        public double SimulationSecondsPerRealSecond;
        public double SimulationTicksPerRealSecond;
    }

    /// <summary>
    /// Bounded, opt-in stress instrumentation. Summary mode keeps only counters
    /// and a deterministic digest. Detailed mode adds bounded event/failure
    /// rings; overflow is recorded rather than allocating an unbounded log.
    /// </summary>
    public sealed class StressTelemetry : MonoBehaviour
    {
        private const int DefaultEventCapacity = 4096;
        private const int DefaultFailureCapacity = 256;
        private const int DefaultFrameSampleCapacity = 4096;
        private const int DefaultActivityDiagnosticCapacity = 64;
        private const int DefaultOwnershipDiagnosticCapacity = 128;
        private const int MaximumUniqueActorsPerActivity = 512;
        private const int MaximumPendingActivityDiagnostics = 1024;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static StressTelemetry Instance { get; private set; }

        [Header("Observation")]
        [SerializeField] private StressObservationMode observationMode = StressObservationMode.Summary;
        [SerializeField, Min(32)] private int eventCapacity = DefaultEventCapacity;
        [SerializeField, Min(16)] private int failureCapacity = DefaultFailureCapacity;

        private long[] counters;
        private StressEventRecord[] eventRing;
        private StressFailureRecord[] failureRing;
        private StressActivityDiagnosticState[] activityDiagnostics;
        private StressOwnershipDiagnostic[] ownershipDiagnostics;
        private int activityDiagnosticCount;
        private int ownershipDiagnosticCount;
        private int[] pendingActorIds;
        private int[] pendingActivityIndexes;
        private long[] pendingActivityTicks;
        private int pendingActivityCount;
        private bool firstFailureCaptured;
        private StressFailureRecord firstFailure;
        private double[] frameSamples;
        private int frameSampleWriteIndex;
        private long frameSampleCount;
        private int eventWriteIndex;
        private int failureWriteIndex;
        private long eventCount;
        private long failureCount;
        private long droppedEvents;
        private long droppedFailures;
        private ulong digest;
        private string runId = "unstarted";
        private double realSeconds;
        private double simulationSeconds;
        private long simulationTicks;
        private long renderedFrames;
        private double minimumFrameSeconds = double.MaxValue;
        private double maximumFrameSeconds;
        private double totalFrameSeconds;
        private bool hasStartBoundary;
        private long startSimulationTick;
        private long endSimulationTick;
        private double startSimulationSeconds;
        private double endSimulationSeconds;
        private double currentAbsoluteSimulationSeconds;
        private double startRealtimeSeconds;
        private double endRealtimeSeconds;

        public StressObservationMode Mode => observationMode;
        public string RunId => runId;
        public ulong Digest => digest;
        public long EventCount => eventCount;
        public long FailureCount => failureCount;
        public long StartSimulationTick => startSimulationTick;
        public long EndSimulationTick => endSimulationTick;
        public double StartSimulationSeconds => startSimulationSeconds;
        public double EndSimulationSeconds => endSimulationSeconds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            AllocateBuffers();
            ResetRun("scene-start");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnValidate()
        {
            eventCapacity = Mathf.Max(32, eventCapacity);
            failureCapacity = Mathf.Max(16, failureCapacity);
        }

        public void Configure(
            StressObservationMode mode,
            int detailedEventCapacity = DefaultEventCapacity,
            int detailedFailureCapacity = DefaultFailureCapacity)
        {
            observationMode = mode;
            eventCapacity = Mathf.Max(32, detailedEventCapacity);
            failureCapacity = Mathf.Max(16, detailedFailureCapacity);
            AllocateBuffers();
        }

        public void ResetRun(string newRunId)
        {
            if (counters == null || eventRing == null || failureRing == null || frameSamples == null)
                AllocateBuffers();

            Array.Clear(counters, 0, counters.Length);
            Array.Clear(eventRing, 0, eventRing.Length);
            Array.Clear(failureRing, 0, failureRing.Length);
            Array.Clear(frameSamples, 0, frameSamples.Length);
            Array.Clear(activityDiagnostics, 0, activityDiagnostics.Length);
            Array.Clear(ownershipDiagnostics, 0, ownershipDiagnostics.Length);
            eventWriteIndex = 0;
            failureWriteIndex = 0;
            eventCount = 0;
            failureCount = 0;
            frameSampleWriteIndex = 0;
            frameSampleCount = 0;
            droppedEvents = 0;
            droppedFailures = 0;
            digest = FnvOffset;
            runId = string.IsNullOrEmpty(newRunId) ? "run" : newRunId;
            realSeconds = 0d;
            simulationSeconds = 0d;
            simulationTicks = 0;
            renderedFrames = 0;
            minimumFrameSeconds = double.MaxValue;
            maximumFrameSeconds = 0d;
            totalFrameSeconds = 0d;
            activityDiagnosticCount = 0;
            ownershipDiagnosticCount = 0;
            pendingActivityCount = 0;
            firstFailureCaptured = false;
            firstFailure = default(StressFailureRecord);
            hasStartBoundary = false;
            startSimulationTick = 0L;
            endSimulationTick = 0L;
            startSimulationSeconds = 0d;
            endSimulationSeconds = 0d;
            currentAbsoluteSimulationSeconds = 0d;
            startRealtimeSeconds = 0d;
            endRealtimeSeconds = 0d;
        }

        public void RecordFrame(double unscaledDeltaSeconds)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            double delta = Math.Max(0d, unscaledDeltaSeconds);
            renderedFrames++;
            realSeconds += delta;
            totalFrameSeconds += delta;
            minimumFrameSeconds = Math.Min(minimumFrameSeconds, delta);
            maximumFrameSeconds = Math.Max(maximumFrameSeconds, delta);
            frameSamples[frameSampleWriteIndex] = delta;
            frameSampleWriteIndex = (frameSampleWriteIndex + 1) % frameSamples.Length;
            frameSampleCount++;
        }

        public void SetSimulationProgress(long tick, double seconds)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            if (!hasStartBoundary)
                BeginBoundary(tick, seconds);

            endSimulationTick = tick;
            currentAbsoluteSimulationSeconds = IsFinite(seconds) ? seconds : endSimulationSeconds;
            endSimulationSeconds = currentAbsoluteSimulationSeconds;
            simulationTicks = Math.Max(0L, tick - startSimulationTick);
            simulationSeconds = Math.Max(0d, endSimulationSeconds - startSimulationSeconds);
        }

        public void Increment(StressMetric metric, long amount = 1)
        {
            if (observationMode == StressObservationMode.Disabled ||
                metric == StressMetric.Count ||
                amount == 0)
            {
                return;
            }

            AddCounter(metric, amount);
        }

        public void RecordActivityEvent(
            StressEventKind kind,
            int actorId,
            int facilityId,
            string activityId,
            string detail,
            long simulationTick,
            double seconds,
            int reservationGeneration = 0,
            string phase = null,
            string brainState = null,
            bool stopRequested = false)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            if (kind == StressEventKind.RunStarted || !hasStartBoundary)
                BeginBoundary(simulationTick, seconds);

            double absoluteSeconds = IsFinite(seconds)
                ? seconds
                : currentAbsoluteSimulationSeconds;
            double elapsedSeconds = Math.Max(0d, absoluteSeconds - startSimulationSeconds);
            endSimulationTick = simulationTick;
            endSimulationSeconds = absoluteSeconds;
            currentAbsoluteSimulationSeconds = absoluteSeconds;
            simulationTicks = Math.Max(0L, simulationTick - startSimulationTick);
            simulationSeconds = elapsedSeconds;

            switch (kind)
            {
                case StressEventKind.ActivityRequested:
                    Increment(StressMetric.ActivityRequests);
                    break;
                case StressEventKind.ActivityReserved:
                    Increment(StressMetric.ReservationsAcquired);
                    break;
                case StressEventKind.NavigationStarted:
                    Increment(StressMetric.NavigationStarts);
                    break;
                case StressEventKind.ActivityStarted:
                    Increment(StressMetric.ActivitiesStarted);
                    break;
                case StressEventKind.ExitStarted:
                    Increment(StressMetric.ExitsStarted);
                    break;
                case StressEventKind.ReservationReleased:
                    Increment(StressMetric.ReservationsReleased);
                    break;
                case StressEventKind.ActivityFailed:
                    Increment(StressMetric.ActivityFailures);
                    break;
                case StressEventKind.ActivityDenied:
                    Increment(StressMetric.ActivityDenied);
                    break;
                case StressEventKind.ActivityCompleted:
                    Increment(StressMetric.ActivityCompleted);
                    break;
            }

            UpdateActivityDiagnostic(
                kind,
                actorId,
                activityId,
                simulationTick);

            Mix(kind);
            Mix(actorId);
            Mix(facilityId);
            MixString(activityId);
            MixString(detail);
            Mix(simulationTick);
            Mix(elapsedSeconds);

            StressEventRecord record = new StressEventRecord
            {
                Sequence = eventCount,
                SimulationTick = simulationTick,
                SimulationSeconds = elapsedSeconds,
                AbsoluteSimulationSeconds = absoluteSeconds,
                ActorId = actorId,
                FacilityId = facilityId,
                ReservationGeneration = reservationGeneration,
                Kind = kind,
                ActivityId = activityId ?? string.Empty,
                Detail = detail ?? string.Empty,
                Phase = phase ?? string.Empty,
                BrainState = brainState ?? string.Empty,
                StopRequested = stopRequested
            };
            eventCount++;

            if (observationMode != StressObservationMode.Detailed)
            {
                if (kind == StressEventKind.RunCompleted)
                    EndBoundary(simulationTick, absoluteSeconds);
                return;
            }

            if (eventCount > eventRing.Length)
            {
                droppedEvents++;
                Increment(StressMetric.DroppedDetailedEvents);
            }
            eventRing[eventWriteIndex] = record;
            eventWriteIndex = (eventWriteIndex + 1) % eventRing.Length;

            if (kind == StressEventKind.RunCompleted)
                EndBoundary(simulationTick, absoluteSeconds);
        }

        public void RecordActivityDenied(
            int actorId,
            int facilityId,
            string activityId,
            string detail,
            long simulationTick,
            double seconds,
            int reservationGeneration = 0,
            string phase = null,
            string brainState = null)
        {
            RecordActivityEvent(
                StressEventKind.ActivityDenied,
                actorId,
                facilityId,
                activityId,
                detail,
                simulationTick,
                seconds,
                reservationGeneration,
                phase,
                brainState);
        }

        public void RecordFailure(
            StressEventKind kind,
            int actorId,
            int facilityId,
            string detail,
            long simulationTick,
            string phase = null,
            string brainState = null,
            int reservationGeneration = 0)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            if (kind == StressEventKind.AnimationFailed)
                Increment(StressMetric.AnimationFailures);
            else if (kind == StressEventKind.ActivityFailed)
                Increment(StressMetric.ActivityFailures);
            Mix(kind);
            Mix(actorId);
            Mix(facilityId);
            MixString(detail);
            Mix(simulationTick);

            StressFailureRecord record = new StressFailureRecord
            {
                Sequence = failureCount,
                SimulationTick = simulationTick,
                SimulationSeconds = Math.Max(
                    0d,
                    currentAbsoluteSimulationSeconds - startSimulationSeconds),
                ActorId = actorId,
                FacilityId = facilityId,
                Kind = kind,
                Detail = BuildFailureDetail(detail, phase, brainState, reservationGeneration)
            };
            failureCount++;

            if (!firstFailureCaptured)
            {
                firstFailureCaptured = true;
                firstFailure = record;
                Increment(StressMetric.FirstFailureCaptured);
            }

            if (observationMode != StressObservationMode.Detailed)
                return;

            if (failureCount > failureRing.Length)
            {
                droppedFailures++;
                Increment(StressMetric.DroppedFailureRecords);
            }
            failureRing[failureWriteIndex] = record;
            failureWriteIndex = (failureWriteIndex + 1) % failureRing.Length;
        }

        public void RecordOwnershipDiagnostic(
            StressMetric metric,
            int actorId,
            int facilityId,
            string reservationGroup,
            int reservationGeneration,
            string detail,
            long simulationTick,
            double seconds)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            Increment(metric);
            RecordFailure(
                StressEventKind.OwnershipViolation,
                actorId,
                facilityId,
                detail,
                simulationTick,
                string.Empty,
                string.Empty,
                reservationGeneration);
            RecordActivityEvent(
                StressEventKind.OwnershipViolation,
                actorId,
                facilityId,
                string.Empty,
                detail,
                simulationTick,
                seconds,
                reservationGeneration,
                string.Empty,
                string.Empty);

            if (ownershipDiagnosticCount >= ownershipDiagnostics.Length)
            {
                Increment(StressMetric.OwnershipDiagnosticsTruncated);
                return;
            }

            ownershipDiagnostics[ownershipDiagnosticCount++] = new StressOwnershipDiagnostic
            {
                Sequence = eventCount,
                SimulationTick = simulationTick,
                ActorId = actorId,
                FacilityId = facilityId,
                ReservationGeneration = reservationGeneration,
                ReservationGroup = reservationGroup ?? string.Empty,
                Detail = detail ?? string.Empty
            };
        }

        public void RecordCoverageDiagnostic(
            string activityId,
            string detail,
            long simulationTick,
            double seconds)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            Increment(StressMetric.MissingActivityCoverage);
            RecordFailure(
                StressEventKind.CoverageViolation,
                0,
                0,
                detail,
                simulationTick);
            RecordActivityEvent(
                StressEventKind.CoverageViolation,
                0,
                0,
                activityId,
                detail,
                simulationTick,
                seconds);
        }

        public StressTelemetrySnapshot CaptureSnapshot()
        {
            StressTelemetrySnapshot snapshot = new StressTelemetrySnapshot
            {
                RunId = runId,
                Mode = observationMode,
                Counters = (long[])counters.Clone(),
                Events = CopyEvents(),
                Failures = CopyFailures(),
                EventCount = eventCount,
                FailureCount = failureCount,
                DroppedEvents = droppedEvents,
                DroppedFailures = droppedFailures,
                ActivityDiagnostics = CopyActivityDiagnostics(),
                OwnershipDiagnostics = CopyOwnershipDiagnostics(),
                Digest = digest,
                StartSimulationTick = startSimulationTick,
                EndSimulationTick = endSimulationTick,
                StartSimulationSeconds = startSimulationSeconds,
                EndSimulationSeconds = endSimulationSeconds,
                ElapsedSimulationSeconds = simulationSeconds,
                StartRealtimeSeconds = startRealtimeSeconds,
                EndRealtimeSeconds = endRealtimeSeconds,
                ElapsedRealtimeSeconds = Math.Max(0d, endRealtimeSeconds - startRealtimeSeconds),
                RealSeconds = realSeconds,
                SimulationSeconds = simulationSeconds,
                SimulationTicks = simulationTicks,
                RenderedFrames = renderedFrames,
                MinimumFrameSeconds = minimumFrameSeconds == double.MaxValue ? 0d : minimumFrameSeconds,
                MaximumFrameSeconds = maximumFrameSeconds,
                AverageFrameSeconds = renderedFrames == 0 ? 0d : totalFrameSeconds / renderedFrames,
                P50FrameSeconds = CalculateFramePercentile(0.50d),
                P95FrameSeconds = CalculateFramePercentile(0.95d),
                P99FrameSeconds = CalculateFramePercentile(0.99d),
                SimulationSecondsPerRealSecond = realSeconds <= 0d ? 0d : simulationSeconds / realSeconds,
                SimulationTicksPerRealSecond = realSeconds <= 0d ? 0d : simulationTicks / realSeconds
            };
            return snapshot;
        }

        public long GetCounter(StressMetric metric)
        {
            if (counters == null || (int)metric < 0 || (int)metric >= counters.Length)
                return 0;
            return counters[(int)metric];
        }

        public bool TryGetActivityDiagnostic(
            string activityId,
            out StressActivityDiagnostic diagnostic)
        {
            StressActivityDiagnosticState state = FindActivityDiagnostic(activityId);
            if (state == null)
            {
                diagnostic = default(StressActivityDiagnostic);
                return false;
            }

            diagnostic = state.ToSnapshot();
            return true;
        }

        private void AllocateBuffers()
        {
            counters = new long[(int)StressMetric.Count];
            eventRing = new StressEventRecord[Mathf.Max(32, eventCapacity)];
            failureRing = new StressFailureRecord[Mathf.Max(16, failureCapacity)];
            frameSamples = new double[DefaultFrameSampleCapacity];
            activityDiagnostics = new StressActivityDiagnosticState[DefaultActivityDiagnosticCapacity];
            ownershipDiagnostics = new StressOwnershipDiagnostic[DefaultOwnershipDiagnosticCapacity];
            pendingActorIds = new int[MaximumPendingActivityDiagnostics];
            pendingActivityIndexes = new int[MaximumPendingActivityDiagnostics];
            pendingActivityTicks = new long[MaximumPendingActivityDiagnostics];
        }

        private void AddCounter(StressMetric metric, long amount)
        {
            counters[(int)metric] += amount;
        }

        private StressEventRecord[] CopyEvents()
        {
            if (observationMode != StressObservationMode.Detailed)
                return Array.Empty<StressEventRecord>();

            int count = (int)Math.Min(eventCount, eventRing.Length);
            StressEventRecord[] result = new StressEventRecord[count];
            int start = eventCount <= eventRing.Length ? 0 : eventWriteIndex;
            for (int i = 0; i < count; i++)
                result[i] = eventRing[(start + i) % eventRing.Length];
            return result;
        }

        private StressFailureRecord[] CopyFailures()
        {
            if (observationMode == StressObservationMode.Summary)
            {
                return firstFailureCaptured
                    ? new[] { firstFailure }
                    : Array.Empty<StressFailureRecord>();
            }

            int count = (int)Math.Min(failureCount, failureRing.Length);
            StressFailureRecord[] result = new StressFailureRecord[count];
            int start = failureCount <= failureRing.Length ? 0 : failureWriteIndex;
            for (int i = 0; i < count; i++)
                result[i] = failureRing[(start + i) % failureRing.Length];
            return result;
        }

        private StressActivityDiagnostic[] CopyActivityDiagnostics()
        {
            StressActivityDiagnostic[] result =
                new StressActivityDiagnostic[activityDiagnosticCount];
            for (int index = 0; index < activityDiagnosticCount; index++)
            {
                StressActivityDiagnosticState state = activityDiagnostics[index];
                result[index] = state.ToSnapshot();
            }

            return result;
        }

        private StressOwnershipDiagnostic[] CopyOwnershipDiagnostics()
        {
            int count = Math.Min(ownershipDiagnosticCount, ownershipDiagnostics.Length);
            StressOwnershipDiagnostic[] result = new StressOwnershipDiagnostic[count];
            Array.Copy(ownershipDiagnostics, result, count);
            return result;
        }

        private double CalculateFramePercentile(double percentile)
        {
            int count = (int)Math.Min(frameSampleCount, frameSamples.Length);
            if (count == 0)
                return 0d;

            double[] sorted = new double[count];
            int start = frameSampleCount <= frameSamples.Length ? 0 : frameSampleWriteIndex;
            for (int i = 0; i < count; i++)
                sorted[i] = frameSamples[(start + i) % frameSamples.Length];
            Array.Sort(sorted);
            int index = (int)Math.Round(
                Mathf.Clamp01((float)percentile) * (count - 1),
                MidpointRounding.AwayFromZero);
            return sorted[index];
        }

        private void BeginBoundary(long tick, double seconds)
        {
            if (hasStartBoundary)
                return;

            hasStartBoundary = true;
            startSimulationTick = tick;
            startSimulationSeconds = IsFinite(seconds) ? seconds : 0d;
            endSimulationTick = tick;
            endSimulationSeconds = startSimulationSeconds;
            currentAbsoluteSimulationSeconds = startSimulationSeconds;
            startRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            endRealtimeSeconds = startRealtimeSeconds;
        }

        private void EndBoundary(long tick, double seconds)
        {
            endSimulationTick = tick;
            endSimulationSeconds = IsFinite(seconds)
                ? seconds
                : currentAbsoluteSimulationSeconds;
            currentAbsoluteSimulationSeconds = endSimulationSeconds;
            simulationTicks = Math.Max(0L, endSimulationTick - startSimulationTick);
            simulationSeconds = Math.Max(0d, endSimulationSeconds - startSimulationSeconds);
            endRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
        }

        private void UpdateActivityDiagnostic(
            StressEventKind kind,
            int actorId,
            string activityId,
            long simulationTick)
        {
            if (string.IsNullOrEmpty(activityId))
                return;

            StressActivityDiagnosticState state = FindActivityDiagnostic(activityId);
            if (state == null)
                return;

            state.LastTick = simulationTick;
            if (state.FirstTick < 0L)
                state.FirstTick = simulationTick;

            switch (kind)
            {
                case StressEventKind.ActivityRequested:
                    state.Requested++;
                    AddPendingActivity(actorId, activityDiagnosticCountFor(state), simulationTick);
                    break;
                case StressEventKind.ActivityDenied:
                    state.Denied++;
                    break;
                case StressEventKind.ActivityReserved:
                    state.Reserved++;
                    break;
                case StressEventKind.NavigationStarted:
                    state.NavigationStarted++;
                    break;
                case StressEventKind.ActivityStarted:
                    state.ActiveStarted++;
                    if (state.AddServedActor(actorId, MaximumUniqueActorsPerActivity))
                        Increment(StressMetric.UniqueActorsServed);
                    RecordPendingWait(actorId, activityDiagnosticCountFor(state), simulationTick, state);
                    break;
                case StressEventKind.ExitStarted:
                    state.ExitStarted++;
                    break;
                case StressEventKind.ReservationReleased:
                    state.Released++;
                    break;
                case StressEventKind.ActivityCompleted:
                    state.Completed++;
                    break;
                case StressEventKind.ActivityFailed:
                    state.Failed++;
                    break;
            }
        }

        private int activityDiagnosticCountFor(StressActivityDiagnosticState state)
        {
            for (int index = 0; index < activityDiagnosticCount; index++)
            {
                if (ReferenceEquals(activityDiagnostics[index], state))
                    return index;
            }

            return -1;
        }

        private StressActivityDiagnosticState FindActivityDiagnostic(string activityId)
        {
            for (int index = 0; index < activityDiagnosticCount; index++)
            {
                if (string.Equals(
                        activityDiagnostics[index].ActivityId,
                        activityId,
                        StringComparison.Ordinal))
                {
                    return activityDiagnostics[index];
                }
            }

            if (activityDiagnosticCount >= activityDiagnostics.Length)
            {
                Increment(StressMetric.CoverageDiagnosticsTruncated);
                return null;
            }

            StressActivityDiagnosticState created =
                new StressActivityDiagnosticState(activityId);
            activityDiagnostics[activityDiagnosticCount++] = created;
            return created;
        }

        private void AddPendingActivity(int actorId, int activityIndex, long tick)
        {
            if (activityIndex < 0)
                return;

            for (int index = pendingActivityCount - 1; index >= 0; index--)
            {
                if (pendingActorIds[index] == actorId &&
                    pendingActivityIndexes[index] == activityIndex)
                {
                    pendingActivityTicks[index] = tick;
                    return;
                }
            }

            if (pendingActivityCount >= pendingActorIds.Length)
            {
                Increment(StressMetric.PendingActivityDiagnosticsTruncated);
                return;
            }

            pendingActorIds[pendingActivityCount] = actorId;
            pendingActivityIndexes[pendingActivityCount] = activityIndex;
            pendingActivityTicks[pendingActivityCount] = tick;
            pendingActivityCount++;
        }

        private void RecordPendingWait(
            int actorId,
            int activityIndex,
            long tick,
            StressActivityDiagnosticState state)
        {
            if (activityIndex < 0)
                return;

            for (int index = 0; index < pendingActivityCount; index++)
            {
                if (pendingActorIds[index] != actorId ||
                    pendingActivityIndexes[index] != activityIndex)
                {
                    continue;
                }

                long wait = Math.Max(0L, tick - pendingActivityTicks[index]);
                state.WaitSamples++;
                state.TotalWaitTicks += wait;
                state.MaximumWaitTicks = Math.Max(state.MaximumWaitTicks, wait);
                pendingActivityCount--;
                pendingActorIds[index] = pendingActorIds[pendingActivityCount];
                pendingActivityIndexes[index] = pendingActivityIndexes[pendingActivityCount];
                pendingActivityTicks[index] = pendingActivityTicks[pendingActivityCount];
                return;
            }
        }

        private static string BuildFailureDetail(
            string detail,
            string phase,
            string brainState,
            int reservationGeneration)
        {
            string result = detail ?? string.Empty;
            if (!string.IsNullOrEmpty(phase))
                result += " phase=" + phase;
            if (!string.IsNullOrEmpty(brainState))
                result += " brain=" + brainState;
            if (reservationGeneration != 0)
                result += " generation=" + reservationGeneration;
            return result;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class StressActivityDiagnosticState
        {
            private readonly int[] servedActorIds = new int[MaximumUniqueActorsPerActivity];
            private int servedActorCount;

            public StressActivityDiagnosticState(string id)
            {
                ActivityId = id ?? string.Empty;
                FirstTick = -1L;
            }

            public string ActivityId;
            public long Requested;
            public long Denied;
            public long Reserved;
            public long NavigationStarted;
            public long ActiveStarted;
            public long ExitStarted;
            public long Released;
            public long Completed;
            public long Failed;
            public long WaitSamples;
            public long TotalWaitTicks;
            public long MaximumWaitTicks;
            public long FirstTick;
            public long LastTick;

            public bool AddServedActor(int actorId, int capacity)
            {
                for (int index = 0; index < servedActorCount; index++)
                {
                    if (servedActorIds[index] == actorId)
                        return false;
                }

                if (servedActorCount >= capacity)
                    return false;

                servedActorIds[servedActorCount++] = actorId;
                return true;
            }

            public StressActivityDiagnostic ToSnapshot()
            {
                return new StressActivityDiagnostic
                {
                    ActivityId = ActivityId,
                    Requested = Requested,
                    Denied = Denied,
                    Reserved = Reserved,
                    NavigationStarted = NavigationStarted,
                    ActiveStarted = ActiveStarted,
                    ExitStarted = ExitStarted,
                    Released = Released,
                    Completed = Completed,
                    Failed = Failed,
                    UniqueActorsServed = servedActorCount,
                    WaitSamples = WaitSamples,
                    TotalWaitTicks = TotalWaitTicks,
                    MaximumWaitTicks = MaximumWaitTicks,
                    FirstTick = FirstTick,
                    LastTick = LastTick
                };
            }
        }

        private void Mix(long value)
        {
            unchecked
            {
                ulong bits = (ulong)value;
                for (int i = 0; i < sizeof(long); i++)
                {
                    digest ^= (byte)(bits & 0xff);
                    digest *= FnvPrime;
                    bits >>= 8;
                }
            }
        }

        private void Mix(ulong value)
        {
            unchecked
            {
                for (int i = 0; i < sizeof(ulong); i++)
                {
                    digest ^= (byte)(value & 0xff);
                    digest *= FnvPrime;
                    value >>= 8;
                }
            }
        }

        private void Mix(StressEventKind value) => Mix((long)value);

        private void Mix(double value)
        {
            Mix((ulong)BitConverter.DoubleToInt64Bits(value));
        }

        private void MixString(string value)
        {
            string source = value ?? string.Empty;
            Mix(source.Length);
            for (int i = 0; i < source.Length; i++)
                Mix((long)source[i]);
        }

        public static string DigestText(ulong value)
        {
            return value.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
