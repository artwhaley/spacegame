using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class OffDutyManagerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private OffDutyManager managerUnderTest;

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null)
                    Object.DestroyImmediate(objects[index]);
            objects.Clear();
            managerUnderTest = null;
        }

        [Test]
        public void BidProducesOfferOnlyWhenManagerResolvesTheRound()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent provider = CreateProvider("Recreation", Vector3.zero, 1f);
            ColonistIdentity alice = CreateIdentity("Alice");

            Assert.That(manager.TryPeekOffer(alice, 1L, out _), Is.False);
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice, OffDutyDrive.Stimulation));
            Assert.That(manager.TryPeekOffer(alice, 1L, out _), Is.False);

            manager.SimulationTick(0.1f);

            Assert.That(manager.TryPeekOffer(alice, 1L, out OffDutyOffer offer), Is.True);
            Assert.That(offer.Opportunity.Provider, Is.SameAs(provider));
            Assert.That(provider.Facility.IsReserved("Play01"), Is.False);
        }

        [Test]
        public void SameReservationGroupIsOfferedToOnlyOneBidderPerRound()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Recreation", Vector3.zero, 1f);
            ColonistIdentity alice = CreateIdentity("Alice");
            ColonistIdentity dana = CreateIdentity("Dana");

            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                dana, OffDutyDrive.Stimulation));
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice, OffDutyDrive.Stimulation));
            manager.SimulationTick(0.1f);

            Assert.That(manager.TryPeekOffer(alice, 1L, out _), Is.True,
                "Stable requester ordering should make Alice the winner.");
            Assert.That(manager.TryPeekOffer(dana, 1L, out _), Is.False);
        }

        [Test]
        public void DeclinedSpecificOpportunityIsSuppressedOnlyForThatRequester()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent dance = CreateProvider(
                "Dance", Vector3.zero, 1f, "dance", "Dance01");
            OffDutyComponent reading = CreateProvider(
                "Reading", new Vector3(3f, 0f, 0f), 1f, "reading", "Reading01");
            ColonistIdentity alice = CreateIdentity("Alice");
            ColonistIdentity dana = CreateIdentity("Dana");

            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice, OffDutyDrive.Stimulation));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryGetOffer(alice, 1L, out OffDutyOffer declined), Is.True);
            Assert.That(declined.Opportunity.Provider, Is.SameAs(dance));
            Assert.That(manager.RemoveOffer(declined, accepted: false, reason: "brain_choice"), Is.True);

            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice, OffDutyDrive.Stimulation));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(alice, 1L, out OffDutyOffer fallback), Is.True);
            Assert.That(fallback.Opportunity.Provider, Is.SameAs(reading));

            manager.RemoveOffer(fallback, accepted: false, reason: "test_cleanup");
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                dana, OffDutyDrive.Stimulation));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(dana, 1L, out OffDutyOffer danaOffer), Is.True);
            Assert.That(danaOffer.Opportunity.Provider, Is.SameAs(dance));
        }

        [Test]
        public void DriveDurationAndCooldownFactsStillFilterOffers()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Play", Vector3.zero, 2f, "play", "Play01");
            ColonistIdentity alice = CreateIdentity("Alice");
            OffDutyCompletionHistory history = new OffDutyCompletionHistory();
            history.RecordCompletion("play", 10f);

            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice,
                OffDutyDrive.Relaxation,
                history,
                maximumSafeDurationGameHours: 1f));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(alice, 1L, out _), Is.False);

            OffDutyComponent valid = CreateProvider(
                "Relax", new Vector3(2f, 0f, 0f), 1f, "relax", "Relax01",
                stimulationRecovery: 0f, relaxationRecovery: 60f);
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice,
                OffDutyDrive.Relaxation,
                history,
                maximumSafeDurationGameHours: 1f));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(alice, 1L, out OffDutyOffer offer), Is.True);
            Assert.That(offer.Opportunity.Provider, Is.SameAs(valid));
        }

        [Test]
        public void StaleOfferIsRemovedWithoutHoldingTheFacility()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent provider = CreateProvider("Recreation", Vector3.zero, 1f);
            ColonistIdentity alice = CreateIdentity("Alice");
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                alice, OffDutyDrive.Stimulation));
            manager.SimulationTick(0.1f);

            Assert.That(manager.TryGetOffer(alice, 2L, out _), Is.False);
            Assert.That(provider.Facility.IsReserved("Play01"), Is.False);
        }

        private OffDutyManager CreateManager()
        {
            GameObject managerObject = new GameObject("OffDuty Manager");
            objects.Add(managerObject);
            managerUnderTest = managerObject.AddComponent<OffDutyManager>();
            return managerUnderTest;
        }

        private OffDutyComponent CreateProvider(
            string name,
            Vector3 position,
            float duration,
            string activityId = "play",
            string reservationGroup = "Play01",
            float stimulationRecovery = 60f,
            float relaxationRecovery = 0f)
        {
            GameObject facilityObject = new GameObject(name);
            facilityObject.transform.position = position;
            objects.Add(facilityObject);
            InteractableFacility facility = facilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject(name + " Approach").transform;
            approach.SetParent(facilityObject.transform, false);
            FacilityActivityBinding facilityBinding = new FacilityActivityBinding();
            SetPrivateField(facilityBinding, "activityId", activityId);
            SetPrivateField(facilityBinding, "reservationGroup", reservationGroup);
            SetPrivateField(facilityBinding, "externallyRequestable", true);
            SetPrivateField(facilityBinding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { facilityBinding });

            OffDutyComponent provider = facilityObject.AddComponent<OffDutyComponent>();
            OffDutyActivityBinding activity = new OffDutyActivityBinding();
            SetPrivateField(activity, "activityId", activityId);
            SetPrivateField(activity, "plannedDurationGameHours", duration);
            SetPrivateField(activity, "enabled", true);
            SetPrivateField(activity, "stimulationRecoveryPerGameHour", stimulationRecovery);
            SetPrivateField(activity, "relaxationRecoveryPerGameHour", relaxationRecovery);
            SetPrivateField(provider, "activities", new[] { activity });
            provider.RefreshStaticBindingMetadata();
            managerUnderTest?.Register(provider);
            return provider;
        }

        private ColonistIdentity CreateIdentity(string displayName)
        {
            return ActivityBidTestHelpers.CreateIdentity(displayName, objects);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
