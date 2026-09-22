using NUnit.Framework;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class DailyShiftWindowTests
    {
        [Test]
        public void OrdinaryAndOvernightWindowsAreValid()
        {
            Assert.That(new DailyShiftWindow(8f, 14f).IsConfigured, Is.True);
            Assert.That(new DailyShiftWindow(22f, 6f).IsConfigured, Is.True);
        }

        [Test]
        public void EqualOrOutOfRangeHoursAreInvalid()
        {
            Assert.That(new DailyShiftWindow(8f, 8f).IsConfigured, Is.False);
            Assert.That(new DailyShiftWindow(-1f, 8f).IsConfigured, Is.False);
            Assert.That(new DailyShiftWindow(0f, 24f).IsConfigured, Is.False);
            Assert.That(new DailyShiftWindow(float.NaN, 8f).IsConfigured, Is.False);
            Assert.That(new DailyShiftWindow(8f, float.PositiveInfinity).IsConfigured, Is.False);
        }

        [Test]
        public void DurationSupportsOrdinaryAndOvernightWindows()
        {
            Assert.That(new DailyShiftWindow(8f, 14f).DurationHours, Is.EqualTo(6f));
            Assert.That(new DailyShiftWindow(8f, 16f).DurationHours, Is.EqualTo(8f));
            Assert.That(new DailyShiftWindow(22f, 6f).DurationHours, Is.EqualTo(8f));
            Assert.That(new DailyShiftWindow(6f, 18f).DurationHours, Is.EqualTo(12f));
        }

        [Test]
        public void ContainsHourUsesHalfOpenOrdinaryInterval()
        {
            DailyShiftWindow shift = new DailyShiftWindow(8f, 14f);

            Assert.That(shift.ContainsHourOfDay(7.999f), Is.False);
            Assert.That(shift.ContainsHourOfDay(8f), Is.True);
            Assert.That(shift.ContainsHourOfDay(13.999f), Is.True);
            Assert.That(shift.ContainsHourOfDay(14f), Is.False);
        }

        [Test]
        public void ContainsHourSupportsOvernightIntervalAndBoundaries()
        {
            DailyShiftWindow shift = new DailyShiftWindow(22f, 6f);

            Assert.That(shift.ContainsHourOfDay(21.999f), Is.False);
            Assert.That(shift.ContainsHourOfDay(22f), Is.True);
            Assert.That(shift.ContainsHourOfDay(23f), Is.True);
            Assert.That(shift.ContainsHourOfDay(2f), Is.True);
            Assert.That(shift.ContainsHourOfDay(5.999f), Is.True);
            Assert.That(shift.ContainsHourOfDay(6f), Is.False);
            Assert.That(shift.ContainsHourOfDay(12f), Is.False);
        }

        [Test]
        public void OverlapHandlesOrdinaryAndOvernightWindows()
        {
            Assert.That(new DailyShiftWindow(8f, 14f).Overlaps(new DailyShiftWindow(10f, 16f)), Is.True);
            Assert.That(new DailyShiftWindow(8f, 14f).Overlaps(new DailyShiftWindow(14f, 20f)), Is.False);
            Assert.That(new DailyShiftWindow(8f, 14f).Overlaps(new DailyShiftWindow(20f, 2f)), Is.False);
            Assert.That(new DailyShiftWindow(22f, 6f).Overlaps(new DailyShiftWindow(5f, 10f)), Is.True);
            Assert.That(new DailyShiftWindow(22f, 6f).Overlaps(new DailyShiftWindow(6f, 12f)), Is.False);
            Assert.That(new DailyShiftWindow(22f, 6f).Overlaps(new DailyShiftWindow(23f, 3f)), Is.True);
        }

        [Test]
        public void InvalidOrNullWindowsDoNotOverlap()
        {
            DailyShiftWindow valid = new DailyShiftWindow(8f, 14f);

            Assert.That(valid.Overlaps(null), Is.False);
            Assert.That(valid.Overlaps(new DailyShiftWindow(8f, 8f)), Is.False);
        }
    }
}
