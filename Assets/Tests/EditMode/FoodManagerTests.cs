using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class FoodManagerTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
            {
                if (sceneObjects[index] != null)
                    Object.DestroyImmediate(sceneObjects[index]);
            }
        }

        [Test]
        public void ManagerDiscoversServiceCreatedBeforeManager()
        {
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            FoodManager manager = CreateManager();

            Assert.That(manager.Services, Does.Contain(service));
            Assert.That(
                manager.TryFindFoodTarget(Vector3.one, out ActivityTarget target),
                Is.True);
            Assert.That(target.Facility, Is.SameAs(service.Facility));
            Assert.That(target.ActivityId, Is.EqualTo("Eat"));
        }

        [Test]
        public void ServiceCreatedAfterManagerRegistersImmediately()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);

            Assert.That(manager.Services, Does.Contain(service));
        }

        [Test]
        public void DiscoveryReturnsNearestLiveService()
        {
            CreateManager();
            FoodServiceComponent far = CreateFoodService("Far Cafeteria", new Vector3(10f, 0f, 0f));
            FoodServiceComponent near = CreateFoodService("Near Cafeteria", new Vector3(2f, 0f, 0f));

            Assert.That(
                FoodManager.Instance.TryFindFoodService(
                    Vector3.zero,
                    out FoodServiceComponent result),
                Is.True);
            Assert.That(result, Is.SameAs(near));
            Assert.That(result, Is.Not.SameAs(far));
        }

        [Test]
        public void DisabledOrReservedServiceIsNotDiscoverable()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            service.enabled = false;

            Assert.That(
                manager.TryFindFoodTarget(Vector3.zero, out _),
                Is.False);

            service.enabled = true;
            Assert.That(
                service.Facility.TryGetBinding("Eat", out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                service.Facility.TryAcquire(binding.ReservationGroup, manager, out _),
                Is.True);
            Assert.That(
                manager.TryFindFoodTarget(Vector3.zero, out _),
                Is.False);
        }

        [Test]
        public void MisconfiguredOrNonExternalServiceIsExcluded()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent misconfigured =
                CreateFoodService("Misconfigured Cafeteria", Vector3.zero);
            SetPrivateField(misconfigured, "eatActivityId", "Missing");
            Assert.That(manager.TryFindFoodTarget(Vector3.zero, out _), Is.False);

            SetPrivateField(misconfigured, "eatActivityId", "Eat");
            Assert.That(
                misconfigured.Facility.TryGetBinding(
                    "Eat", out FacilityActivityBinding binding),
                Is.True);
            SetPrivateField(binding, "externallyRequestable", false);
            Assert.That(manager.TryFindFoodTarget(Vector3.zero, out _), Is.False);
        }

        [Test]
        public void EqualDistanceSelectionIsDeterministic()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent first =
                CreateFoodService("First Cafeteria", new Vector3(2f, 0f, 0f));
            FoodServiceComponent second =
                CreateFoodService("Second Cafeteria", new Vector3(-2f, 0f, 0f));

            Assert.That(
                manager.TryFindFoodService(Vector3.zero, out FoodServiceComponent selected),
                Is.True);
            Assert.That(selected, Is.SameAs(first));

            Assert.That(
                manager.TryFindFoodService(Vector3.zero, out FoodServiceComponent repeated),
                Is.True);
            Assert.That(repeated, Is.SameAs(selected));
        }

        [Test]
        public void DiscoveryDoesNotReserveTheSelectedFacility()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);

            Assert.That(manager.TryFindFoodTarget(Vector3.zero, out _), Is.True);
            Assert.That(service.Facility.IsReserved("Eat01"), Is.False);
        }

        [Test]
        public void LiveTargetRemainsValidWhenItsReservationIsHeld()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            ActivityTarget target = new ActivityTarget(service.Facility, "Eat");

            Assert.That(
                service.Facility.TryAcquire("Eat01", manager, out _),
                Is.True);
            Assert.That(manager.IsFoodTargetLive(target), Is.True);
        }

        [Test]
        public void DestroyedServiceDisappearsFromRegistry()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            Assert.That(manager.Services, Does.Contain(service));

            Object.DestroyImmediate(service.gameObject);

            Assert.That(manager.TryFindFoodTarget(Vector3.zero, out _), Is.False);
        }

        private FoodManager CreateManager()
        {
            GameObject managerObject = new GameObject("Food Manager");
            sceneObjects.Add(managerObject);
            return managerObject.AddComponent<FoodManager>();
        }

        private FoodServiceComponent CreateFoodService(string name, Vector3 position)
        {
            GameObject serviceObject = new GameObject(name);
            serviceObject.transform.position = position;
            sceneObjects.Add(serviceObject);

            InteractableFacility facility = serviceObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject(name + " Approach").transform;
            approach.SetParent(serviceObject.transform, false);

            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "Eat");
            SetPrivateField(binding, "reservationGroup", "Eat01");
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { binding });

            FoodServiceComponent service = serviceObject.AddComponent<FoodServiceComponent>();
            SetPrivateField(service, "eatActivityId", "Eat");
            SetPrivateField(service, "hungerRecoveryPerGameHour", 60f);
            return service;
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
