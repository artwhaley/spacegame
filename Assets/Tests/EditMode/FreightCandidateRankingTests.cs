using NUnit.Framework;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class FreightCandidateRankingTests
    {
        [Test]
        public void CandidateRankingUsesQuantityThenDistanceThenStableKey()
        {
            int quantityFirst = FreightCandidateRanking.Compare(
                10f, 100f, "farther", 5f, 1f, "closer");
            int distanceSecond = FreightCandidateRanking.Compare(
                10f, 4f, "farther", 10f, 8f, "closer");
            int stableKeyThird = FreightCandidateRanking.Compare(
                10f, 4f, "A", 10f, 4f, "B");

            Assert.That(quantityFirst, Is.LessThan(0), "The larger useful quantity must win even when farther.");
            Assert.That(distanceSecond, Is.LessThan(0), "Distance breaks equal-quantity ties.");
            Assert.That(stableKeyThird, Is.LessThan(0), "Stable ordinal identity breaks exact ties.");
        }
    }
}
