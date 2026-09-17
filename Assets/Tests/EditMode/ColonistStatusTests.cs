using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistStatusTests
    {
        private StaffingTestHarness harness;

        [SetUp]
        public void SetUp()
        {
            harness = new StaffingTestHarness();
        }

        [TearDown]
        public void TearDown()
        {
            harness.Dispose();
        }

        [Test]
        public void WorkAndSleepUseConfiguredRatesAndModifiers()
        {
            LocationAnchor home = harness.New("Home").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Worker", home);
            ColonistStatusComponent status = colonist.Status;

            status.ApplyWorkFatigue(1f, 1.5f);
            Assert.That(status.Fatigue, Is.EqualTo(0.15f).Within(0.0001f));
            status.ApplySleepRecovery(1f, 1.5f);
            Assert.That(status.Fatigue, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ExhaustionLatchClearsAtConfiguredRecoveryThreshold()
        {
            ColonistAgent colonist = harness.Colonist("Worker", null);
            ColonistStatusComponent status = colonist.Status;

            Assert.That(status.recoveredThreshold, Is.EqualTo(0.20f).Within(0.0001f));
            status.AdjustFatigue(0.9f);
            Assert.That(status.IsExhausted, Is.True);
            status.AdjustFatigue(-0.65f);
            Assert.That(status.Fatigue, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(status.IsExhausted, Is.True);
            status.AdjustFatigue(-0.05f);
            Assert.That(status.Fatigue, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(status.IsExhausted, Is.False);
        }

        [Test]
        public void DutyHistoryStoresActualWorkAndEndReason()
        {
            WorkerClassDefinition workerClass = harness.Class("Worker");
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.requiredClass = workerClass;
            role.maximumAssignedPerShift = 1;
            StaffingComponent workplace = harness.Facility(
                "Workplace", null, harness.Pattern(("A", 0f, 8f)), role);
            ColonistAgent colonist = harness.Colonist("Worker", null, workerClass);
            ColonistStatusComponent status = colonist.Status;

            Assert.That(status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReleasedResting));

            status.BeginDuty(workplace, role, "A", 2f);
            Assert.That(status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReleasedResting));
            Assert.That(status.ActiveDuty.fatigueAtStart, Is.EqualTo(0f).Within(0.0001f));
            status.SetDutyState(ColonistDutyState.AcceptingNewWork);
            status.AdjustFatigue(0.3f);
            status.RequestDutyRelease(4f, DutyEndReason.ShiftEnded);
            Assert.That(status.CurrentDutyState, Is.EqualTo(ColonistDutyState.AcceptingNewWork));
            status.AddWorkedTime(3f);
            status.SetDutyState(ColonistDutyState.CompletingCommittedWork);
            status.SetDutyState(ColonistDutyState.ReturningHome);
            Assert.That(status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReturningHome));
            status.EndDuty(5f, DutyEndReason.Exhausted);
            status.SetDutyState(ColonistDutyState.ReleasedResting);
            Assert.That(status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReleasedResting));

            Assert.That(status.DutyHistory.Count, Is.EqualTo(1));
            Assert.That(status.DutyHistory[0].workedGameHours, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(status.DutyHistory[0].endReason, Is.EqualTo(DutyEndReason.Exhausted));
            Assert.That(status.DutyHistory[0].fatigueAtReleaseRequest, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(status.DutyHistory[0].fatigueAtEnd, Is.EqualTo(0.3f).Within(0.0001f));
        }
    }
}
