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
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private FoodManager managerUnderTest;

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
                if (sceneObjects[index] != null)
                    Object.DestroyImmediate(sceneObjects[index]);
            for (int index = assets.Count - 1; index >= 0; index--)
                if (assets[index] != null)
                    Object.DestroyImmediate(assets[index]);
            sceneObjects.Clear();
            assets.Clear();
            managerUnderTest = null;
        }

        [Test]
        public void BidProducesOfferOnlyWhenFoodManagerResolvesTheRound()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            ColonistIdentity bob = CreateIdentity("Bob");

            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out _), Is.False);
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out _), Is.False);

            manager.SimulationTick(0.1f);

            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out FoodOffer offer), Is.True);
            Assert.That(offer.Opportunity.Service, Is.SameAs(service));
            Assert.That(service.Facility.IsReserved("Eat01"), Is.False,
                "Publishing an offer must not reserve the physical seat.");
        }

        [Test]
        public void CriticalHungerWinsTheSingleSeatOffer()
        {
            FoodManager manager = CreateManager();
            CreateFoodService("Cafeteria", Vector3.zero);
            ColonistIdentity ordinary = CreateIdentity("Charlie");
            ColonistIdentity critical = CreateIdentity("Bob");

            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(
                ordinary, hunger: 90f, isCritical: false));
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(
                critical, hunger: 95f, isCritical: true));
            manager.SimulationTick(0.1f);

            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(critical, nextBrainTick, out _), Is.True);
            Assert.That(manager.TryPeekOffer(ordinary, nextBrainTick, out _), Is.False);
        }

        [Test]
        public void HigherHungerWinsWhenCriticalityTies()
        {
            FoodManager manager = CreateManager();
            CreateFoodService("Cafeteria", Vector3.zero);
            ColonistIdentity low = CreateIdentity("Charlie");
            ColonistIdentity high = CreateIdentity("Bob");

            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(low, hunger: 80f, isCritical: true));
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(high, hunger: 90f, isCritical: true));
            manager.SimulationTick(0.1f);

            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(high, nextBrainTick, out _), Is.True);
            Assert.That(manager.TryPeekOffer(low, nextBrainTick, out _), Is.False);
        }

        [Test]
        public void StaleOfferExpiresWithoutCreatingAReservation()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            ColonistIdentity bob = CreateIdentity("Bob");
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            manager.SimulationTick(0.1f);

            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryGetOffer(bob, nextBrainTick + 1L, out _), Is.False);
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out _), Is.False);
            Assert.That(service.Facility.IsReserved("Eat01"), Is.False);
        }

        [Test]
        public void EmptyInventoryProducesNoOfferAndNewStockIsSeenNextRound()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService(
                "Cafeteria", Vector3.zero, inventoryAccounting: true, startingFood: 0f);
            ColonistIdentity bob = CreateIdentity("Bob");

            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            manager.SimulationTick(0.1f);
            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out _), Is.False);
            Assert.That(service.FoodAvailable, Is.EqualTo(0f));

            Assert.That(service.FoodInventory.Add(service.FoodResource, 1f), Is.EqualTo(1f));
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            manager.SimulationTick(0.1f);

            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out FoodOffer offer), Is.True);
            Assert.That(offer.Opportunity.Service, Is.SameAs(service));
        }

        [Test]
        public void MealHoldReservesThenReleasesOrCommitsExactlyOnce()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService(
                "Cafeteria", Vector3.zero, inventoryAccounting: true, startingFood: 1f);
            ColonistIdentity bob = CreateIdentity("Bob");
            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            manager.SimulationTick(0.1f);
            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out FoodOffer offer), Is.True);

            GameObject runnerObject = new GameObject("Bob Runner");
            sceneObjects.Add(runnerObject);
            ColonistActivityRunner runner = runnerObject.AddComponent<ColonistActivityRunner>();
            Assert.That(service.Facility.TryAcquire("Eat01", runner, out FacilityReservationToken token), Is.True);
            SetPrivateField(runner, "currentFacility", service.Facility);
            SetPrivateField(runner, "reservation", token);

            Assert.That(manager.TryReserveMeal(offer, runner, out FoodMealCommitment commitment), Is.True);
            Assert.That(service.FoodReserved, Is.EqualTo(1f));
            Assert.That(service.FoodAvailable, Is.EqualTo(0f));

            manager.ReleaseMeal(commitment);
            manager.ReleaseMeal(commitment);
            Assert.That(service.FoodReserved, Is.EqualTo(0f));
            Assert.That(service.FoodOnHand, Is.EqualTo(1f));

            FoodMealCommitment second = CreateCommitment(manager, offer, runner);
            Assert.That(manager.CommitMeal(second), Is.True);
            Assert.That(manager.CommitMeal(second), Is.True);
            Assert.That(service.FoodOnHand, Is.EqualTo(0f));
            Assert.That(service.FoodReserved, Is.EqualTo(0f));
        }

        [Test]
        public void DisabledServiceCannotReceiveABidOffer()
        {
            FoodManager manager = CreateManager();
            FoodServiceComponent service = CreateFoodService("Cafeteria", Vector3.zero);
            ColonistIdentity bob = CreateIdentity("Bob");
            service.enabled = false;

            manager.SubmitBid(ActivityBidTestHelpers.CreateFoodBid(bob));
            manager.SimulationTick(0.1f);

            long nextBrainTick = ActivityBidTestHelpers.CurrentTick + 1L;
            Assert.That(manager.TryPeekOffer(bob, nextBrainTick, out _), Is.False);
        }

        private FoodMealCommitment CreateCommitment(
            FoodManager manager,
            FoodOffer offer,
            ColonistActivityRunner runner)
        {
            Assert.That(manager.TryReserveMeal(offer, runner, out FoodMealCommitment commitment), Is.True);
            return commitment;
        }

        private FoodManager CreateManager()
        {
            GameObject managerObject = new GameObject("Food Manager");
            sceneObjects.Add(managerObject);
            managerUnderTest = managerObject.AddComponent<FoodManager>();
            return managerUnderTest;
        }

        private FoodServiceComponent CreateFoodService(
            string name,
            Vector3 position,
            bool inventoryAccounting = true,
            float startingFood = 20f)
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
            SetPrivateField(service, "inventoryAccountingEnabled", true);
            {
                InventoryComponent inventory = serviceObject.AddComponent<InventoryComponent>();
                ResourceDefinition food = ScriptableObject.CreateInstance<ResourceDefinition>();
                food.stableId = name + "Food";
                food.quantityMode = ResourceQuantityMode.Discrete;
                food.hungerRecoveryPerUnit = 90f;
                food.consumptionDurationGameHours = 0.25f;
                assets.Add(food);
                ConfigureInventory(inventory, food, 20f, startingFood);
                SetPrivateField(service, "foodInventory", inventory);
                SetPrivateField(service, "foodResource", food);
                service.RefreshStaticBindingMetadata();
            }
            managerUnderTest?.Register(service);
            return service;
        }

        private ColonistIdentity CreateIdentity(string displayName)
        {
            return ActivityBidTestHelpers.CreateIdentity(displayName, sceneObjects);
        }

        private static void ConfigureInventory(
            InventoryComponent inventory,
            ResourceDefinition resource,
            float capacity,
            float onHand)
        {
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry
            {
                resource = resource,
                capacity = capacity,
                onHand = onHand
            });
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

    internal static class ActivityBidTestHelpers
    {
        public static long CurrentTick =>
            SimulationManager.Instance != null ? SimulationManager.Instance.CurrentTick : 0L;

        public static FoodBid CreateFoodBid(
            ColonistIdentity requester,
            float hunger = 80f,
            bool isCritical = false,
            int preferenceRank = 0,
            long outstandingNeedAge = 1L,
            float? gameHour = null)
        {
            return new FoodBid(
                requester,
                requester != null ? requester.transform.position : Vector3.zero,
                gameHour ?? (SimulationManager.Instance != null
                    ? SimulationManager.Instance.CurrentGameHour
                    : 0f),
                hunger,
                isCritical,
                preferenceRank,
                CurrentTick,
                outstandingNeedAge);
        }

        public static OffDutyBid CreateOffDutyBid(
            ColonistIdentity requester,
            OffDutyDrive drive,
            OffDutyCompletionHistory completionHistory = null,
            int preferenceRank = 0,
            float maximumSafeDurationGameHours = 4f)
        {
            return new OffDutyBid(
                requester,
                requester != null ? requester.transform.position : Vector3.zero,
                drive,
                maximumSafeDurationGameHours,
                completionHistory,
                SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f,
                preferenceRank,
                CurrentTick);
        }

        public static ColonistIdentity CreateIdentity(
            string displayName,
            ICollection<GameObject> objects)
        {
            GameObject identityObject = new GameObject(displayName);
            objects.Add(identityObject);
            ColonistIdentity identity = identityObject.AddComponent<ColonistIdentity>();
            FieldInfo field = typeof(ColonistIdentity).GetField(
                "displayName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(identity, displayName);
            return identity;
        }
    }
}
