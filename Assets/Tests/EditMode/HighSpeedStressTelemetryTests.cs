using Colony.Interactions;
using AsteroidColony.Stress;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class HighSpeedStressTelemetryTests
    {
        private GameObject telemetryObject;

        [TearDown]
        public void TearDown()
        {
            if (telemetryObject != null)
                Object.DestroyImmediate(telemetryObject);
        }

        [Test]
        public void StableIdentityHashIsIndependentOfUnityInstanceId()
        {
            int first = Stress.StressStableIdentity.StableHash("colonist_007");
            int second = Stress.StressStableIdentity.StableHash("colonist_007");
            int different = Stress.StressStableIdentity.StableHash("colonist_008");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(different, Is.Not.EqualTo(first));
        }

        [Test]
        public void SummaryTelemetryDigestIsRepeatable()
        {
            StressTelemetrySnapshot first = CaptureSummary("same");
            Object.DestroyImmediate(telemetryObject);
            telemetryObject = null;
            StressTelemetrySnapshot second = CaptureSummary("same");

            Assert.That(first.Digest, Is.EqualTo(second.Digest));
            Assert.That(first.Events, Is.Empty);
            Assert.That(first.Failures, Is.Empty);
        }

        [Test]
        public void DetailedTelemetryRetainsBoundedNewestEventsAndReportsOverflow()
        {
            telemetryObject = new GameObject("Stress Telemetry");
            StressTelemetry telemetry = telemetryObject.AddComponent<StressTelemetry>();
            telemetry.Configure(StressObservationMode.Detailed, 32, 16);
            telemetry.ResetRun("bounded");

            for (int i = 0; i < 40; i++)
            {
                telemetry.RecordActivityEvent(
                    StressEventKind.ActivityStarted,
                    i,
                    1,
                    "Work",
                    string.Empty,
                    i,
                    i);
            }

            StressTelemetrySnapshot snapshot = telemetry.CaptureSnapshot();
            Assert.That(snapshot.Events.Length, Is.EqualTo(32));
            Assert.That(snapshot.DroppedEvents, Is.EqualTo(8));
            Assert.That(snapshot.Events[0].ActorId, Is.EqualTo(8));
            Assert.That(snapshot.Events[31].ActorId, Is.EqualTo(39));
        }

        [Test]
        public void ComparatorReportsTheFirstRetainedEventDifference()
        {
            StressTelemetrySnapshot left = CaptureDetailed("left", 1);
            Object.DestroyImmediate(telemetryObject);
            telemetryObject = null;
            StressTelemetrySnapshot right = CaptureDetailed("right", 2);

            Assert.That(
                StressRunComparator.TryCompare(left, right, out string difference),
                Is.False);
            Assert.That(difference, Does.Contain("first retained event difference"));
        }

        private StressTelemetrySnapshot CaptureSummary(string runId)
        {
            telemetryObject = new GameObject("Stress Telemetry");
            StressTelemetry telemetry = telemetryObject.AddComponent<StressTelemetry>();
            telemetry.Configure(StressObservationMode.Summary, 32, 16);
            telemetry.ResetRun(runId);
            telemetry.RecordActivityEvent(
                StressEventKind.ActivityStarted,
                7,
                2,
                "Work",
                "same",
                10,
                10d);
            return telemetry.CaptureSnapshot();
        }

        private StressTelemetrySnapshot CaptureDetailed(string runId, int actorId)
        {
            telemetryObject = new GameObject("Stress Telemetry");
            StressTelemetry telemetry = telemetryObject.AddComponent<StressTelemetry>();
            telemetry.Configure(StressObservationMode.Detailed, 32, 16);
            telemetry.ResetRun(runId);
            telemetry.RecordActivityEvent(
                StressEventKind.ActivityStarted,
                actorId,
                2,
                "Work",
                "difference",
                10,
                10d);
            return telemetry.CaptureSnapshot();
        }
    }
}
