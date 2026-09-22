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
        public int ActorId;
        public int FacilityId;
        public StressEventKind Kind;
        public string ActivityId;
        public string Detail;
    }

    [Serializable]
    public struct StressFailureRecord
    {
        public long Sequence;
        public long SimulationTick;
        public int ActorId;
        public int FacilityId;
        public StressEventKind Kind;
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
        public ulong Digest;
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

        public StressObservationMode Mode => observationMode;
        public string RunId => runId;
        public ulong Digest => digest;
        public long EventCount => eventCount;
        public long FailureCount => failureCount;

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

            simulationTicks = tick;
            simulationSeconds = seconds;
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
            double seconds)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

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
            }

            Mix(kind);
            Mix(actorId);
            Mix(facilityId);
            MixString(activityId);
            MixString(detail);
            Mix(simulationTick);
            Mix(seconds);

            if (observationMode != StressObservationMode.Detailed)
                return;

            StressEventRecord record = new StressEventRecord
            {
                Sequence = eventCount,
                SimulationTick = simulationTick,
                SimulationSeconds = seconds,
                ActorId = actorId,
                FacilityId = facilityId,
                Kind = kind,
                ActivityId = activityId ?? string.Empty,
                Detail = detail ?? string.Empty
            };
            if (eventCount >= eventRing.Length)
            {
                droppedEvents++;
                Increment(StressMetric.DroppedDetailedEvents);
            }
            eventRing[eventWriteIndex] = record;
            eventWriteIndex = (eventWriteIndex + 1) % eventRing.Length;
            eventCount++;
        }

        public void RecordFailure(
            StressEventKind kind,
            int actorId,
            int facilityId,
            string detail,
            long simulationTick)
        {
            if (observationMode == StressObservationMode.Disabled)
                return;

            Increment(kind == StressEventKind.AnimationFailed
                ? StressMetric.AnimationFailures
                : StressMetric.ActivityFailures);
            Mix(kind);
            Mix(actorId);
            Mix(facilityId);
            MixString(detail);
            Mix(simulationTick);

            if (observationMode != StressObservationMode.Detailed)
                return;

            StressFailureRecord record = new StressFailureRecord
            {
                Sequence = failureCount,
                SimulationTick = simulationTick,
                ActorId = actorId,
                FacilityId = facilityId,
                Kind = kind,
                Detail = detail ?? string.Empty
            };
            if (failureCount >= failureRing.Length)
            {
                droppedFailures++;
                Increment(StressMetric.DroppedFailureRecords);
            }
            failureRing[failureWriteIndex] = record;
            failureWriteIndex = (failureWriteIndex + 1) % failureRing.Length;
            failureCount++;
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
                Digest = digest,
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

        private void AllocateBuffers()
        {
            counters = new long[(int)StressMetric.Count];
            eventRing = new StressEventRecord[Mathf.Max(32, eventCapacity)];
            failureRing = new StressFailureRecord[Mathf.Max(16, failureCapacity)];
            frameSamples = new double[DefaultFrameSampleCapacity];
        }

        private void AddCounter(StressMetric metric, long amount)
        {
            counters[(int)metric] += amount;
        }

        private StressEventRecord[] CopyEvents()
        {
            int count = (int)Math.Min(eventCount, eventRing.Length);
            StressEventRecord[] result = new StressEventRecord[count];
            int start = eventCount <= eventRing.Length ? 0 : eventWriteIndex;
            for (int i = 0; i < count; i++)
                result[i] = eventRing[(start + i) % eventRing.Length];
            return result;
        }

        private StressFailureRecord[] CopyFailures()
        {
            int count = (int)Math.Min(failureCount, failureRing.Length);
            StressFailureRecord[] result = new StressFailureRecord[count];
            int start = failureCount <= failureRing.Length ? 0 : failureWriteIndex;
            for (int i = 0; i < count; i++)
                result[i] = failureRing[(start + i) % failureRing.Length];
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
