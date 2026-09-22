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

        [Test]
        public void RunBoundaryNormalizesAbsoluteClockToElapsedTime()
        {
            telemetryObject = new GameObject("Stress Telemetry");
            StressTelemetry telemetry = telemetryObject.AddComponent<StressTelemetry>();
            telemetry.Configure(StressObservationMode.Detailed, 32, 16);
            telemetry.ResetRun("elapsed");
            telemetry.RecordActivityEvent(
                StressEventKind.RunStarted, 0, 0, string.Empty, string.Empty, 500, 28800d);
            telemetry.RecordActivityEvent(
                StressEventKind.ActivityStarted, 7, 2, "Work", string.Empty, 510, 28810d);
            telemetry.RecordActivityEvent(
                StressEventKind.RunCompleted, 0, 0, string.Empty, string.Empty, 520, 28820d);

            StressTelemetrySnapshot snapshot = telemetry.CaptureSnapshot();
            Assert.That(snapshot.StartSimulationTick, Is.EqualTo(500));
            Assert.That(snapshot.EndSimulationTick, Is.EqualTo(520));
            Assert.That(snapshot.StartSimulationSeconds, Is.EqualTo(28800d));
            Assert.That(snapshot.ElapsedSimulationSeconds, Is.EqualTo(20d));
            Assert.That(snapshot.Events[1].SimulationSeconds, Is.EqualTo(10d));
            Assert.That(snapshot.Events[1].AbsoluteSimulationSeconds, Is.EqualTo(28810d));
        }

        [Test]
        public void SummaryModeRetainsFirstFailureWitness()
        {
            telemetryObject = new GameObject("Stress Telemetry");
            StressTelemetry telemetry = telemetryObject.AddComponent<StressTelemetry>();
            telemetry.Configure(StressObservationMode.Summary, 32, 16);
            telemetry.ResetRun("summary-failure");
            telemetry.RecordActivityEvent(
                StressEventKind.RunStarted, 0, 0, string.Empty, string.Empty, 10, 100d);
            telemetry.RecordFailure(
                StressEventKind.AnimationFailed, 7, 2, "first witness", 12,
                "entry", "Working", 3);
            telemetry.RecordFailure(
                StressEventKind.ActivityFailed, 8, 4, "later failure", 13);

            StressTelemetrySnapshot snapshot = telemetry.CaptureSnapshot();
            Assert.That(snapshot.FailureCount, Is.EqualTo(2));
            Assert.That(snapshot.Failures, Has.Length.EqualTo(1));
            Assert.That(snapshot.Failures[0].ActorId, Is.EqualTo(7));
            Assert.That(snapshot.Failures[0].Detail, Does.Contain("first witness"));
            Assert.That(snapshot.Failures[0].Detail, Does.Contain("phase=entry"));
            Assert.That(snapshot.Failures[0].Detail, Does.Contain("generation=3"));
        }

        [Test]
        public void ComparatorRejectsDifferentSimulationBoundariesBeforeDigest()
        {
            StressTelemetrySnapshot left = CaptureDetailed("left-boundary", 1);
            Object.DestroyImmediate(telemetryObject);
            telemetryObject = null;
            StressTelemetrySnapshot right = CaptureDetailed("right-boundary", 1);
            right.EndSimulationTick++;

            Assert.That(
                StressRunComparator.TryCompare(left, right, out string difference),
                Is.False);
            Assert.That(difference, Does.Contain("simulation boundary tick differs"));
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
