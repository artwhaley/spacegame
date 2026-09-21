using NUnit.Framework;

namespace AsteroidColony.Tests
{
    public class OffDutyCompletionHistoryTests
    {
        private OffDutyCompletionHistory history;

        [SetUp]
        public void SetUp()
        {
            history = new OffDutyCompletionHistory();
        }

        [Test]
        public void CompletionStartsTheCooldown()
        {
            history.RecordCompletion("play", 10f);

            Assert.That(history.IsOnCooldown("play", 12f, 10f), Is.True);
            Assert.That(history.IsOnCooldown("play", 12f, 21.9f), Is.True);
            Assert.That(
                history.TryGetRemainingCooldown(
                    "play",
                    12f,
                    10f,
                    out float remaining),
                Is.True);
            Assert.That(remaining, Is.EqualTo(12f).Within(0.0001f));
        }

        [Test]
        public void CooldownExpiresAfterTheAuthoredDuration()
        {
            history.RecordCompletion("play", 10f);

            Assert.That(history.IsOnCooldown("play", 12f, 22f), Is.False);
            Assert.That(
                history.TryGetRemainingCooldown(
                    "play",
                    12f,
                    22f,
                    out float remaining),
                Is.True);
            Assert.That(remaining, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void DifferentKeysDoNotShareACooldown()
        {
            history.RecordCompletion("play", 10f);

            Assert.That(history.IsOnCooldown("play", 12f, 11f), Is.True);
            Assert.That(history.IsOnCooldown("bowling", 12f, 11f), Is.False);
        }

        [Test]
        public void ZeroCooldownNeverBlocks()
        {
            history.RecordCompletion("play", 10f);

            Assert.That(history.IsOnCooldown("play", 0f, 10f), Is.False);
            Assert.That(history.IsOnCooldown("play", 12f, float.NaN), Is.False);
        }

        [Test]
        public void RepeatedCompletionReplacesThePreviousTimestamp()
        {
            history.RecordCompletion("play", 10f);
            history.RecordCompletion("play", 30f);

            Assert.That(history.Records.Count, Is.EqualTo(1));
            Assert.That(history.TryGetLastCompletion("play", out float completedAt), Is.True);
            Assert.That(completedAt, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(history.IsOnCooldown("play", 12f, 35f), Is.True);
        }

        [Test]
        public void InvalidKeysAndHoursAreIgnored()
        {
            history.RecordCompletion(null, 10f);
            history.RecordCompletion("   ", 10f);
            history.RecordCompletion("play", float.NaN);
            history.RecordCompletion("play", float.PositiveInfinity);

            Assert.That(history.Records.Count, Is.EqualTo(0));
            Assert.That(history.IsOnCooldown("play", 12f, 10f), Is.False);
        }

        [Test]
        public void KeysAreTrimmedBeforeStorage()
        {
            history.RecordCompletion("  play  ", 10f);

            Assert.That(history.IsOnCooldown("play", 12f, 11f), Is.True);
        }

        [Test]
        public void CooldownUntilReportsTheExpiryHour()
        {
            history.RecordCompletion("play", 10f);

            Assert.That(
                history.TryGetCooldownUntil("play", 12f, out float cooldownUntil),
                Is.True);
            Assert.That(cooldownUntil, Is.EqualTo(22f).Within(0.0001f));
        }

        [Test]
        public void ClearRemovesAllRecords()
        {
            history.RecordCompletion("play", 10f);
            history.Clear();

            Assert.That(history.Records.Count, Is.EqualTo(0));
            Assert.That(history.IsOnCooldown("play", 12f, 10f), Is.False);
        }
    }
}
