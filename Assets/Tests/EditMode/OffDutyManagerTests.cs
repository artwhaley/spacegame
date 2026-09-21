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

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                    Object.DestroyImmediate(objects[index]);
            }
            objects.Clear();
        }

        [Test]
        public void ProviderCreatedBeforeManagerIsRegistered()
        {
            OffDutyComponent provider = CreateProvider("Recreation", Vector3.zero, 1f);
            OffDutyManager manager = CreateManager();

            Assert.That(manager.Providers, Does.Contain(provider));
            Assert.That(
                manager.TryFindOpportunity(Vector3.zero, 1f, out OffDutyOpportunity opportunity),
                Is.True);
            Assert.That(opportunity.Provider, Is.SameAs(provider));
        }

        [Test]
        public void NearestFittingOpportunityWinsWithoutReservation()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent far = CreateProvider("Far Recreation", new Vector3(10f, 0f, 0f), 1f);
            OffDutyComponent near = CreateProvider("Near Recreation", new Vector3(2f, 0f, 0f), 1f);

            Assert.That(
                manager.TryFindOpportunity(Vector3.zero, 1f, out OffDutyOpportunity opportunity),
                Is.True);
            Assert.That(opportunity.Provider, Is.SameAs(near));
            Assert.That(
                near.Facility.IsReserved("Play01"),
                Is.False);
            Assert.That(far, Is.Not.Null);
        }

        [Test]
        public void DisabledReservedAndTooLongActivitiesAreExcluded()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent disabled = CreateProvider("Disabled", Vector3.zero, 1f);
            OffDutyActivityBinding disabledBinding = disabled.Activities[0];
            SetPrivateField(disabledBinding, "enabled", false);

            OffDutyComponent tooLong = CreateProvider("Too Long", new Vector3(1f, 0f, 0f), 2f);
            OffDutyComponent reserved = CreateProvider("Reserved", new Vector3(2f, 0f, 0f), 1f);
            Assert.That(
                reserved.Facility.TryAcquire("Play01", manager, out _),
                Is.True);

            Assert.That(
                manager.TryFindOpportunity(Vector3.zero, 1f, out OffDutyOpportunity opportunity),
                Is.False);
            Assert.That(opportunity, Is.Null);
            Assert.That(tooLong, Is.Not.Null);
        }

        private OffDutyManager CreateManager()
        {
            GameObject managerObject = new GameObject("OffDuty Manager Test");
            objects.Add(managerObject);
            return managerObject.AddComponent<OffDutyManager>();
        }

        [Test]
        public void StimulatingActivityDoesNotSatisfyRelaxationDrive()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Recreation", Vector3.zero, 1f, 60f, 0f, "play", 12f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, null, 0f),
                    out OffDutyOpportunity stimulating),
                Is.True);
            Assert.That(stimulating.Activity.ActivityId, Is.EqualTo("play"));

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Relaxation, null, 0f),
                    out OffDutyOpportunity relaxing),
                Is.False);
            Assert.That(relaxing, Is.Null);
        }

        [Test]
        public void ActivitySatisfyingBothDrivesIsEligibleForEitherDrive()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Walk", Vector3.zero, 1f, 10f, 30f, "walk", 12f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, null, 0f),
                    out _),
                Is.True);
            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Relaxation, null, 0f),
                    out _),
                Is.True);
        }

        [Test]
        public void CoolingActivityIsExcludedUntilTheCooldownExpires()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Recreation", Vector3.zero, 1f, 60f, 0f, "play", 12f);
            OffDutyCompletionHistory history = new OffDutyCompletionHistory();
            history.RecordCompletion("play", 10f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, history, 15f),
                    out _),
                Is.False);
            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, history, 22f),
                    out _),
                Is.True);
        }

        [Test]
        public void DifferentCooldownKeyRemainsEligible()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Bowling", Vector3.zero, 1f, 60f, 0f, "bowling", 12f);
            OffDutyCompletionHistory history = new OffDutyCompletionHistory();
            history.RecordCompletion("play", 10f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, history, 11f),
                    out OffDutyOpportunity opportunity),
                Is.True);
            Assert.That(opportunity.Activity.CooldownKey, Is.EqualTo("bowling"));
        }

        [Test]
        public void SearchReportExplainsACooldownBlockedSearch()
        {
            OffDutyManager manager = CreateManager();
            CreateProvider("Recreation", Vector3.zero, 1f, 60f, 0f, "play", 12f);
            OffDutyCompletionHistory history = new OffDutyCompletionHistory();
            history.RecordCompletion("play", 10f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, history, 11f),
                    out _,
                    out OffDutySearchReport report),
                Is.False);
            Assert.That(report.ExcludedByCooldown, Is.EqualTo(1));
            Assert.That(report.NoTargetReason, Is.EqualTo("cooldown"));
        }

        [Test]
        public void SearchReportFallsBackToAggregateReasonWhenMixedExclusionsApply()
        {
            OffDutyManager manager = CreateManager();
            OffDutyComponent cooling = CreateProvider(
                "Cooling Recreation",
                Vector3.zero,
                1f,
                60f,
                0f,
                "play",
                12f);
            CreateProvider(
                "Reserved Recreation",
                new Vector3(1f, 0f, 0f),
                1f,
                60f,
                0f,
                "bowling",
                12f);
            Assert.That(
                cooling.Facility.TryAcquire("Play01", manager, out _),
                Is.True);
            OffDutyCompletionHistory history = new OffDutyCompletionHistory();
            history.RecordCompletion("play", 10f);

            Assert.That(
                manager.TryFindOpportunity(
                    CreateQuery(Vector3.zero, 1f, OffDutyDrive.Stimulation, history, 11f),
                    out _,
                    out OffDutySearchReport report),
                Is.False);
            Assert.That(report.ExcludedByCooldown, Is.EqualTo(1));
            Assert.That(report.NoTargetReason, Is.EqualTo("no_fitting_opportunity"));
        }

        private static OffDutyQuery CreateQuery(
            Vector3 position,
            float duration,
            OffDutyDrive drive,
            OffDutyCompletionHistory history,
            float currentGameHour)
        {
            return new OffDutyQuery(
                null,
                position,
                duration,
                drive,
                history,
                currentGameHour);
        }

        private OffDutyComponent CreateProvider(string name, Vector3 position, float duration)
        {
            return CreateProvider(name, position, duration, 0f, 0f, null, 12f);
        }

        private OffDutyComponent CreateProvider(
            string name,
            Vector3 position,
            float duration,
            float stimulationRecovery,
            float relaxationRecovery,
            string cooldownKey,
            float cooldownHours)
        {
            GameObject facilityObject = new GameObject(name);
            facilityObject.transform.position = position;
            objects.Add(facilityObject);

            InteractableFacility facility = facilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject(name + " Approach").transform;
            approach.SetParent(facilityObject.transform, false);
            FacilityActivityBinding facilityBinding = new FacilityActivityBinding();
            SetPrivateField(facilityBinding, "activityId", "play");
            SetPrivateField(facilityBinding, "reservationGroup", "Play01");
            SetPrivateField(facilityBinding, "externallyRequestable", true);
            SetPrivateField(facilityBinding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { facilityBinding });

            OffDutyComponent provider = facilityObject.AddComponent<OffDutyComponent>();
            OffDutyActivityBinding activity = new OffDutyActivityBinding();
            SetPrivateField(activity, "activityId", "play");
            SetPrivateField(activity, "plannedDurationGameHours", duration);
            SetPrivateField(activity, "enabled", true);
            SetPrivateField(activity, "cooldownGameHours", cooldownHours);
            SetPrivateField(activity, "cooldownKey", cooldownKey);
            SetPrivateField(activity, "stimulationRecoveryPerGameHour", stimulationRecovery);
            SetPrivateField(activity, "relaxationRecoveryPerGameHour", relaxationRecovery);
            SetPrivateField(provider, "activities", new[] { activity });
            OffDutyManager.Instance?.Register(provider);
            return provider;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
