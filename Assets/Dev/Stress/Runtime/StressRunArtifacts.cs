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
        public string FormatVersion = "HSS-1";
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
            builder.AppendLine("sequence,simulation_tick,simulation_seconds,actor_id,facility_id,kind,activity,detail");
            if (snapshot == null || snapshot.Events == null)
                return builder.ToString();

            for (int i = 0; i < snapshot.Events.Length; i++)
            {
                StressEventRecord record = snapshot.Events[i];
                builder.Append(record.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.SimulationTick.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.SimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.ActorId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.FacilityId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.Kind).Append(',')
                    .Append(Csv(record.ActivityId)).Append(',')
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
            }

            if (snapshot != null)
            {
                builder.AppendLine();
                builder.Append("- Real seconds observed: `").Append(snapshot.RealSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Simulation seconds observed: `").Append(snapshot.SimulationSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Simulation ticks: `").Append(snapshot.SimulationTicks.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Detailed events retained: `").Append(snapshot.Events != null ? snapshot.Events.Length : 0).AppendLine("`");
                builder.Append("- Detailed events dropped: `").Append(snapshot.DroppedEvents.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Failures retained: `").Append(snapshot.Failures != null ? snapshot.Failures.Length : 0).AppendLine("`");
                builder.Append("- Failures dropped: `").Append(snapshot.DroppedFailures.ToString(CultureInfo.InvariantCulture)).AppendLine("`");
                builder.Append("- Frame p50/p95/p99: `").Append(snapshot.P50FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.P95FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(snapshot.P99FrameSeconds.ToString("R", CultureInfo.InvariantCulture)).AppendLine(" seconds`");
                builder.Append("- Simulation throughput: `").Append(snapshot.SimulationSecondsPerRealSecond.ToString("R", CultureInfo.InvariantCulture)).AppendLine(" simulated seconds per real second`");
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
            StressTelemetrySnapshot left,
            StressTelemetrySnapshot right,
            out string firstDifference)
        {
            if (left == null || right == null)
            {
                firstDifference = "One snapshot is null.";
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
                if (left.Counters[i] != right.Counters[i])
                {
                    firstDifference = "Counter " + ((StressMetric)i) + " differs.";
                    return false;
                }
            }

            firstDifference = string.Empty;
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
                    left[i].ActorId != right[i].ActorId ||
                    left[i].FacilityId != right[i].FacilityId ||
                    !string.Equals(left[i].ActivityId, right[i].ActivityId, StringComparison.Ordinal) ||
                    !string.Equals(left[i].Detail, right[i].Detail, StringComparison.Ordinal))
                {
                    difference = "first retained event difference at index " + i;
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
    }
}
