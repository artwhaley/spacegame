using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AsteroidColony.Stress
{
    [Serializable]
    public sealed class StressRunManifest
    {
        public string FormatVersion = "HSS-2";
        public string RunId;
        public string Scenario;
        public string SourceScene;
        public string ObservationMode;
        public int Population;
        public int SpeedMultiplier;
        public float PresentationSpeedFactor;
        public float LogicalStepSimulationSeconds;
        public int MaxLogicalStepsPerFrame;
        public int EventCapacity;
        public int FailureCapacity;
        public string UnityVersion;
        public string Digest;
        public long StartSimulationTick;
        public long EndSimulationTick;
        public double StartSimulationSeconds;
        public double EndSimulationSeconds;
        public double ElapsedSimulationSeconds;
        public double RequestedSimulationSeconds;
        public double CommittedSimulationSeconds;
        public double PaceShortfallSeconds;
        public double MaximumObservedDebtSeconds;
        public long AdmissionLimitHitCount;
    }

    public static class StressRunExporter
    {
        public static string Export(
            string directory,
            StressRunManifest manifest,
            StressTelemetrySnapshot snapshot)
        {
            if (string.IsNullOrEmpty(directory))
                directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);

            if (manifest != null && snapshot != null)
            {
                manifest.StartSimulationTick = snapshot.StartSimulationTick;
                manifest.EndSimulationTick = snapshot.EndSimulationTick;
                manifest.StartSimulationSeconds = snapshot.StartSimulationSeconds;
                manifest.EndSimulationSeconds = snapshot.EndSimulationSeconds;
                manifest.ElapsedSimulationSeconds = snapshot.ElapsedSimulationSeconds;
                manifest.Digest = StressTelemetry.DigestText(snapshot.Digest);
            }

            string safeId = Sanitize(manifest != null ? manifest.RunId : "run");
            string stem = "high_speed_" + safeId;
            string jsonPath = Path.Combine(directory, stem + ".json");
            string csvPath = Path.Combine(directory, stem + "_events.csv");
            string reportPath = Path.Combine(directory, stem + ".md");

            ExportBundle bundle = new ExportBundle
            {
                Manifest = manifest,
                Snapshot = snapshot
            };
            File.WriteAllText(jsonPath, JsonUtility.ToJson(bundle, true));
            File.WriteAllText(csvPath, BuildEventCsv(snapshot));
            File.WriteAllText(reportPath, BuildMarkdownReport(manifest, snapshot));
            return jsonPath;
        }

        private static string BuildEventCsv(StressTelemetrySnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("sequence,simulation_tick,simulation_seconds,absolute_simulation_seconds,actor_id,facility_id,reservation_generation,kind,activity,phase,brain_state,stop_requested,detail");
            if (snapshot == null || snapshot.Events == null)
                return builder.ToString();

            for (int i = 0; i < snapshot.Events.Length; i++)
            {
                StressEventRecord record = snapshot.Events[i];
                builder.Append(record.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.SimulationTick.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.SimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.AbsoluteSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.ActorId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.FacilityId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.ReservationGeneration.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.Kind).Append(',')
                    .Append(Csv(record.ActivityId)).Append(',')
                    .Append(Csv(record.Phase)).Append(',')
                    .Append(Csv(record.BrainState)).Append(',')
                    .Append(record.StopRequested ? "1" : "0").Append(',')
                    .Append(Csv(record.Detail)).AppendLine();
            }

            return builder.ToString();
        }

        private static string BuildMarkdownReport(
            StressRunManifest manifest,
            StressTelemetrySnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# High-Speed Stress Run");
            builder.AppendLine();
            builder.AppendLine("This artifact was exported after the run; no file I/O occurs on the simulation tick path.");
            builder.AppendLine();
            if (manifest != null)
            {
                builder.Append("- Run: `").Append(manifest.RunId).AppendLine("`");
                builder.Append("- Scenario: `").Append(manifest.Scenario).AppendLine("`");
                builder.Append("- Speed multiplier: `").Append(manifest.SpeedMultiplier).AppendLine("x`");
                builder.Append("- Logical step: `").Append(manifest.LogicalStepSimulationSeconds.ToString(CultureInfo.InvariantCulture)).AppendLine(" simulated seconds`");
                builder.Append("- Digest: `").Append(manifest.Digest).AppendLine("`");
                builder.Append("- Simulation boundary: ticks `").Append(manifest.StartSimulationTick).Append(" -> ")
                    .Append(manifest.EndSimulationTick).Append("`, seconds `")
                    .Append(manifest.StartSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" -> ")
                    .Append(manifest.EndSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Requested / committed / shortfall: `")
                    .Append(manifest.RequestedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(manifest.CommittedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(manifest.PaceShortfallSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine(" simulated seconds`");
                builder.Append("- Maximum pending debt / admission-limit frames: `")
                    .Append(manifest.MaximumObservedDebtSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(manifest.AdmissionLimitHitCount.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
            }

            if (snapshot != null)
            {
                builder.AppendLine();
                builder.Append("- Real seconds observed: `").Append(snapshot.RealSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Elapsed simulation seconds: `").Append(snapshot.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Simulation ticks: `").Append(snapshot.SimulationTicks.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Semantic events total/retained: `").Append(snapshot.EventCount.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.Events != null ? snapshot.Events.Length : 0).AppendLine("`");
                builder.Append("- Detailed events dropped: `").Append(snapshot.DroppedEvents.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Failures total/retained: `").Append(snapshot.FailureCount.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.Failures != null ? snapshot.Failures.Length : 0).AppendLine("`");
                builder.Append("- Failures dropped: `").Append(snapshot.DroppedFailures.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Frame p50/p95/p99: `").Append(snapshot.P50FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.P95FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.P99FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine(" seconds`");
                builder.Append("- Simulation throughput: `").Append(snapshot.SimulationSecondsPerRealSecond.ToString("R", CultureInfo.InvariantCulture)).AppendLine(" simulated seconds per real second`");
                builder.Append("- Activity diagnostics: `").Append(snapshot.ActivityDiagnostics != null ? snapshot.ActivityDiagnostics.Length : 0).AppendLine("`");
                builder.Append("- Ownership diagnostics: `").Append(snapshot.OwnershipDiagnostics != null ? snapshot.OwnershipDiagnostics.Length : 0).AppendLine("`");
            }

            return builder.ToString();
        }

        private static string Csv(string value)
        {
            string source = value ?? string.Empty;
            if (source.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return source;
            return "\"" + source.Replace("\"", "\"\"") + "\"";
        }

        private static string Sanitize(string value)
        {
            string source = string.IsNullOrEmpty(value) ? "run" : value;
            StringBuilder result = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                result.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_');
            }
            return result.ToString();
        }

        [Serializable]
        private sealed class ExportBundle
        {
            public StressRunManifest Manifest;
            public StressTelemetrySnapshot Snapshot;
        }
    }

    public static class StressRunComparator
    {
        public static bool TryCompare(
            StressRunManifest leftManifest,
            StressTelemetrySnapshot left,
            StressRunManifest rightManifest,
            StressTelemetrySnapshot right,
            out string firstDifference)
        {
            if (!TryCompareManifests(leftManifest, rightManifest, out firstDifference))
                return false;

            return TryCompare(left, right, out firstDifference);
        }

        public static bool TryCompare(
            StressTelemetrySnapshot left,
            StressTelemetrySnapshot right,
            out string firstDifference)
        {
            if (left == null || right == null)
            {
                firstDifference = "One snapshot is null.";
                return false;
            }

            if (left.StartSimulationTick != right.StartSimulationTick ||
                left.EndSimulationTick != right.EndSimulationTick)
            {
                firstDifference = "simulation boundary tick differs: " +
                    left.StartSimulationTick + ".." + left.EndSimulationTick + " vs " +
                    right.StartSimulationTick + ".." + right.EndSimulationTick;
                return false;
            }

            if (!NearlyEqual(left.ElapsedSimulationSeconds, right.ElapsedSimulationSeconds))
            {
                firstDifference = "elapsed simulation time differs: " +
                    left.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture) +
                    " vs " + right.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture);
                return false;
            }

            if (left.Digest != right.Digest)
            {
                firstDifference = "Digest differs: " +
                    StressTelemetry.DigestText(left.Digest) + " vs " +
                    StressTelemetry.DigestText(right.Digest);
                if (TryFindEventDifference(left.Events, right.Events, out string eventDifference))
                    firstDifference += "; " + eventDifference;
                return false;
            }

            if (left.Counters == null || right.Counters == null || left.Counters.Length != right.Counters.Length)
            {
                firstDifference = "Counter shape differs.";
                return false;
            }

            for (int i = 0; i < left.Counters.Length; i++)
            {
                if (!IsSemanticMetric((StressMetric)i))
                    continue;

                if (left.Counters[i] != right.Counters[i])
                {
                    firstDifference = "Counter " + ((StressMetric)i) + " differs.";
                    return false;
                }
            }

            if (left.EventCount != right.EventCount)
            {
                firstDifference = "total semantic event count differs: " +
                    left.EventCount + " vs " + right.EventCount;
                return false;
            }

            if (left.FailureCount != right.FailureCount)
            {
                firstDifference = "total failure count differs: " +
                    left.FailureCount + " vs " + right.FailureCount;
                return false;
            }

            if (left.Events == null || right.Events == null ||
                (left.EventCount > 0L &&
                 (left.Events.Length == 0 || right.Events.Length == 0)))
            {
                firstDifference = "event tail is unavailable for semantic comparison.";
                return false;
            }

            if (TryFindEventDifference(left.Events, right.Events, out string tailDifference))
            {
                firstDifference = tailDifference;
                return false;
            }

            if (left.Failures == null || right.Failures == null ||
                (left.FailureCount > 0L &&
                 (left.Failures.Length == 0 || right.Failures.Length == 0)))
            {
                firstDifference = "failure tail is unavailable for semantic comparison.";
                return false;
            }

            if (TryFindFailureDifference(left.Failures, right.Failures, out string failureDifference))
            {
                firstDifference = failureDifference;
                return false;
            }

            firstDifference = string.Empty;
            return true;
        }

        private static bool TryCompareManifests(
            StressRunManifest left,
            StressRunManifest right,
            out string difference)
        {
            if (left == null || right == null)
            {
                difference = "A comparison manifest is missing.";
                return false;
            }

            if (!string.Equals(left.FormatVersion, right.FormatVersion, StringComparison.Ordinal) ||
                !string.Equals(left.Scenario, right.Scenario, StringComparison.Ordinal) ||
                left.Population != right.Population ||
                !NearlyEqual(left.LogicalStepSimulationSeconds, right.LogicalStepSimulationSeconds) ||
                left.MaxLogicalStepsPerFrame != right.MaxLogicalStepsPerFrame ||
                left.EventCapacity != right.EventCapacity ||
                left.FailureCapacity != right.FailureCapacity ||
                !string.Equals(left.ObservationMode, right.ObservationMode, StringComparison.Ordinal))
            {
                difference = "comparison manifests are incompatible.";
                return false;
            }

            difference = string.Empty;
            return true;
        }

        private static bool TryFindEventDifference(
            StressEventRecord[] left,
            StressEventRecord[] right,
            out string difference)
        {
            int leftCount = left != null ? left.Length : 0;
            int rightCount = right != null ? right.Length : 0;
            int shared = Math.Min(leftCount, rightCount);
            for (int i = 0; i < shared; i++)
            {
                if (left[i].Kind != right[i].Kind ||
                    left[i].Sequence != right[i].Sequence ||
                    left[i].SimulationTick != right[i].SimulationTick ||
                    !NearlyEqual(left[i].SimulationSeconds, right[i].SimulationSeconds) ||
                    left[i].ActorId != right[i].ActorId ||
                    left[i].FacilityId != right[i].FacilityId ||
                    left[i].ReservationGeneration != right[i].ReservationGeneration ||
                    left[i].StopRequested != right[i].StopRequested ||
                    !string.Equals(left[i].ActivityId, right[i].ActivityId, StringComparison.Ordinal) ||
                    !string.Equals(left[i].Detail, right[i].Detail, StringComparison.Ordinal) ||
                    !string.Equals(left[i].Phase, right[i].Phase, StringComparison.Ordinal) ||
                    !string.Equals(left[i].BrainState, right[i].BrainState, StringComparison.Ordinal))
                {
                    difference = "first retained event difference at index " + i +
                        " (tick " + left[i].SimulationTick + " vs " + right[i].SimulationTick +
                        ", time " + left[i].SimulationSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        " vs " + right[i].SimulationSeconds.ToString("R", CultureInfo.InvariantCulture) + ")";
                    return true;
                }
            }

            if (leftCount != rightCount)
            {
                difference = "retained event count differs at index " + shared;
                return true;
            }

            difference = string.Empty;
            return false;
        }

        private static bool TryFindFailureDifference(
            StressFailureRecord[] left,
            StressFailureRecord[] right,
            out string difference)
        {
            int leftCount = left != null ? left.Length : 0;
            int rightCount = right != null ? right.Length : 0;
            int shared = Math.Min(leftCount, rightCount);
            for (int i = 0; i < shared; i++)
            {
                if (left[i].Sequence != right[i].Sequence ||
                    left[i].SimulationTick != right[i].SimulationTick ||
                    !NearlyEqual(left[i].SimulationSeconds, right[i].SimulationSeconds) ||
                    left[i].ActorId != right[i].ActorId ||
                    left[i].FacilityId != right[i].FacilityId ||
                    left[i].Kind != right[i].Kind ||
                    !string.Equals(left[i].Detail, right[i].Detail, StringComparison.Ordinal))
                {
                    difference = "first retained failure difference at index " + i +
                        " (tick " + left[i].SimulationTick + " vs " + right[i].SimulationTick + ")";
                    return true;
                }
            }

            if (leftCount != rightCount)
            {
                difference = "retained failure count differs at index " + shared;
                return true;
            }

            difference = string.Empty;
            return false;
        }

        private static bool IsSemanticMetric(StressMetric metric)
        {
            switch (metric)
            {
                case StressMetric.RenderedFrames:
                case StressMetric.FoodQueries:
                case StressMetric.FoodCandidateEvaluations:
                case StressMetric.FoodSelections:
                case StressMetric.OffDutyQueries:
                case StressMetric.OffDutyCandidateEvaluations:
                case StressMetric.DroppedDetailedEvents:
                case StressMetric.DroppedFailureRecords:
                    return false;
                default:
                    return true;
            }
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= 0.000001d;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= 0.000001f;
        }
    }
}
