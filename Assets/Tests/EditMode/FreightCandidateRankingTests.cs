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

        [Test]
        public void ParentDemandAccountsForMixedChildrenAndReturnsOnlyUntouchedCancellationToUncovered()
        {
            var demand = new FreightDemandAccounting(40f);
            Assert.That(demand.TryCommit(20f), Is.True);
            Assert.That(demand.TryCommit(10f), Is.True);
            Assert.That(demand.TryCommit(5f), Is.True);
            Assert.That(demand.TryCommit(5f), Is.True);
            Assert.That(demand.Committed, Is.EqualTo(40f));
            Assert.That(demand.Uncovered, Is.EqualTo(0f));

            Assert.That(demand.RecordDelivery(5f), Is.True);
            Assert.That(demand.RecordDelivery(20f), Is.True);
            Assert.That(demand.RecordDelivery(10f), Is.True);
            Assert.That(demand.Delivered, Is.EqualTo(35f));
            Assert.That(demand.Committed, Is.EqualTo(5f));

            // Intermediate staging is a custody change and deliberately has no ledger operation.
            Assert.That(demand.Delivered, Is.EqualTo(35f));
            Assert.That(demand.Committed, Is.EqualTo(5f));

            Assert.That(demand.ReleaseCommitment(5f), Is.True);
            Assert.That(demand.Delivered, Is.EqualTo(35f));
            Assert.That(demand.Committed, Is.EqualTo(0f));
            Assert.That(demand.Uncovered, Is.EqualTo(5f));
            Assert.That(demand.IsBalanced, Is.True);
        }

        [Test]
        public void ParentDemandRejectsCommitmentOrDeliveryBeyondItsLiveBalance()
        {
            var demand = new FreightDemandAccounting(40f);
            Assert.That(demand.TryCommit(41f), Is.False);
            Assert.That(demand.TryCommit(20f), Is.True);
            Assert.That(demand.RecordDelivery(21f), Is.False);
            Assert.That(demand.Committed, Is.EqualTo(20f));
            Assert.That(demand.Delivered, Is.EqualTo(0f));
            Assert.That(demand.Uncovered, Is.EqualTo(20f));
        }

        [Test]
        public void TargetReductionKeepsAcceptedChildrenAndOnlyReducesUncoveredDemand()
        {
            var demand = new FreightDemandAccounting(40f);
            Assert.That(demand.TryCommit(10f), Is.True);

            Assert.That(demand.SetUncovered(5f), Is.True);
            Assert.That(demand.Requested, Is.EqualTo(40f));
            Assert.That(demand.Committed, Is.EqualTo(10f));
            Assert.That(demand.Uncovered, Is.EqualTo(5f));
            Assert.That(demand.Retired, Is.EqualTo(25f));

            Assert.That(demand.RecordDelivery(10f), Is.True);
            Assert.That(demand.Delivered, Is.EqualTo(10f));
            Assert.That(demand.Uncovered, Is.EqualTo(5f));
            Assert.That(demand.Retired, Is.EqualTo(25f));
            Assert.That(demand.IsBalanced, Is.True);
        }

        [Test]
        public void RetiringDemandSeparatesCancelledRemainderFromAcceptedWork()
        {
            var demand = new FreightDemandAccounting(40f);
            Assert.That(demand.TryCommit(10f), Is.True);
            Assert.That(demand.RetireUncovered(), Is.EqualTo(30f));
            Assert.That(demand.Uncovered, Is.EqualTo(0f));

            Assert.That(demand.ReleaseCommitment(10f), Is.True);
            Assert.That(demand.RetireUncovered(), Is.EqualTo(10f));
            Assert.That(demand.Delivered, Is.EqualTo(0f));
            Assert.That(demand.Retired, Is.EqualTo(40f));
            Assert.That(demand.IsBalanced, Is.True);
        }

        [Test]
        public void ShipmentSizingAllowsRepeatedAndHeterogeneousProviderCapacityToMeetOneDemand()
        {
            float[] providerCapacities = { 20f, 10f, 5f, 5f };
            float[] childShipments = new float[providerCapacities.Length];
            var demand = new FreightDemandAccounting(40f);
            float uncovered = demand.Uncovered;

            for (int index = 0; index < providerCapacities.Length; index++)
            {
                float child = FreightShipmentSizing.LimitToCapacity(
                    uncovered, uncovered, uncovered, providerCapacities[index], false);
                Assert.That(child, Is.EqualTo(providerCapacities[index]));
                Assert.That(demand.TryCommit(child), Is.True);
                childShipments[index] = child;
                uncovered -= child;
            }

            Assert.That(uncovered, Is.EqualTo(0f));
            Assert.That(demand.Requested, Is.EqualTo(40f));
            Assert.That(demand.Committed, Is.EqualTo(40f));
            Assert.That(demand.Uncovered, Is.EqualTo(0f));
            Assert.That(demand.IsBalanced, Is.True);

            // Different providers can complete children in any order against the same parent.
            Assert.That(demand.RecordDelivery(childShipments[2]), Is.True);
            Assert.That(demand.RecordDelivery(childShipments[0]), Is.True);
            Assert.That(demand.RecordDelivery(childShipments[1]), Is.True);
            Assert.That(demand.Delivered, Is.EqualTo(35f));
            Assert.That(demand.Committed, Is.EqualTo(5f));
            Assert.That(demand.RecordDelivery(childShipments[3]), Is.True);
            Assert.That(demand.Delivered, Is.EqualTo(40f));
            Assert.That(demand.Committed, Is.EqualTo(0f));
            Assert.That(demand.IsBalanced, Is.True);
        }

        [Test]
        public void OneAcceptedTwentyUnitShipmentRemainsCommittedAcrossFourFiveUnitLoads()
        {
            var demand = new FreightDemandAccounting(20f);
            Assert.That(demand.TryCommit(20f), Is.True);

            for (int load = 1; load <= 4; load++)
            {
                Assert.That(demand.RecordDelivery(5f), Is.True);
                Assert.That(demand.Delivered, Is.EqualTo(load * 5f));
                Assert.That(demand.Committed, Is.EqualTo(20f - load * 5f));
                Assert.That(demand.Uncovered, Is.EqualTo(0f));
                Assert.That(demand.IsBalanced, Is.True);
            }

            Assert.That(demand.Delivered, Is.EqualTo(20f));
            Assert.That(demand.Committed, Is.EqualTo(0f));
        }

        [Test]
        public void DiscreteShipmentDoesNotUseFractionalSharedCapacityButContinuousCargoCan()
        {
            Assert.That(FreightShipmentSizing.LimitToCapacity(
                1f, 1f, 0.6f, 1f, true), Is.EqualTo(0f));
            Assert.That(FreightShipmentSizing.LimitToCapacity(
                1f, 1f, 0.6f, 1f, false), Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void ShipmentSizingDoesNotRejectAProviderLegalTailBelowBatchPreference()
        {
            float finalRemainder = FreightShipmentSizing.LimitToCapacity(
                3f, 3f, 10f, 10f, false);
            Assert.That(finalRemainder, Is.EqualTo(3f));
        }
    }
}
