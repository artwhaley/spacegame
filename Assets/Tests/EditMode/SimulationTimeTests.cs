using NUnit.Framework;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class SimulationTimeTests
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
        public void HourOfDayUsesTwentyFourHourBoundaries()
        {
            Assert.That(SimulationTime.DayIndexAt(0f), Is.EqualTo(0));
            Assert.That(SimulationTime.DayNumberAt(0f), Is.EqualTo(1));
            Assert.That(SimulationTime.HourOfDayAt(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(SimulationTime.HourOfDayAt(23.999f), Is.EqualTo(23.999f).Within(0.001f));
            Assert.That(SimulationTime.HourOfDayAt(24f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(SimulationTime.DayNumberAt(24f), Is.EqualTo(2));
            Assert.That(SimulationTime.HourOfDayAt(47.999f), Is.EqualTo(23.999f).Within(0.001f));
            Assert.That(SimulationTime.DayNumberAt(48f), Is.EqualTo(3));
            Assert.That(SimulationTime.HourOfDayAt(49.25f), Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void NegativePureClockInputUsesPositiveModulo()
        {
            Assert.That(SimulationTime.HourOfDayAt(-1f), Is.EqualTo(23f).Within(0.0001f));
            Assert.That(SimulationTime.DayIndexAt(-1f), Is.EqualTo(-1));
        }

        [Test]
        public void TimestampFormattingUsesDayAndTwentyFourHourTime()
        {
            Assert.That(SimulationTime.FormatTimestamp(0f), Is.EqualTo("Day 1 00:00"));
            Assert.That(SimulationTime.FormatTimestamp(8.10f), Is.EqualTo("Day 1 08:06"));
            Assert.That(SimulationTime.FormatTimestamp(23.999f), Is.EqualTo("Day 2 00:00"));
            Assert.That(SimulationTime.FormatTimestamp(24f), Is.EqualTo("Day 2 00:00"));
            Assert.That(SimulationTime.FormatTimestamp(49.25f), Is.EqualTo("Day 3 01:15"));
            Assert.That(SimulationTime.FormatHourOfDay(23.999f), Is.EqualTo("00:00"));
        }

        [Test]
        public void ManagerExposesCalendarViewWithoutWrappingElapsedHours()
        {
            SimulationManager clock = harness.New("Simulation").AddComponent<SimulationManager>();
            harness.SetGameHour(clock, 49.25f);

            Assert.That(clock.CurrentGameHour, Is.EqualTo(49.25f).Within(0.0001f));
            Assert.That(clock.CurrentDayIndex, Is.EqualTo(2));
            Assert.That(clock.CurrentDayNumber, Is.EqualTo(3));
            Assert.That(clock.CurrentHourOfDay, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void SimulationLogUsesCalendarPrefixWithoutChangingMessage()
        {
            SimulationManager clock = harness.New("Simulation").AddComponent<SimulationManager>();
            harness.SetGameHour(clock, 49.25f);
            SimulationLog log = harness.New("Log").AddComponent<SimulationLog>();
            log.simulationManager = clock;

            SimulationLog.Log("clock check");

            Assert.That(log.Entries[log.Entries.Count - 1], Is.EqualTo("[Day 3 01:15] clock check"));
        }
    }
}
