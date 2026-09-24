using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class InventoryComponentTests
    {
        private GameObject inventoryObject;
        private GameObject destinationObject;
        private InventoryComponent inventory;
        private ResourceDefinition food;
        private ResourceDefinition discrete;

        [SetUp]
        public void SetUp()
        {
            inventoryObject = new GameObject("Inventory Test");
            inventory = inventoryObject.AddComponent<InventoryComponent>();
            food = ScriptableObject.CreateInstance<ResourceDefinition>();
            food.name = "Food Test";
            discrete = ScriptableObject.CreateInstance<ResourceDefinition>();
            discrete.name = "Discrete Test";
            discrete.quantityMode = ResourceQuantityMode.Discrete;
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry
            {
                resource = food,
                capacity = 10f
            });
            entries.Add(new InventoryEntry
            {
                resource = discrete,
                capacity = 10f
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (destinationObject != null)
                Object.DestroyImmediate(destinationObject);
            Object.DestroyImmediate(inventoryObject);
            Object.DestroyImmediate(food);
            Object.DestroyImmediate(discrete);
        }

        [Test]
        public void AddRespectsCapacity()
        {
            Assert.That(inventory.Add(food, 12f), Is.EqualTo(10f));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(10f));
            Assert.That(inventory.GetAvailable(food), Is.EqualTo(10f));
        }

        [Test]
        public void ReserveCannotExceedAvailable()
        {
            inventory.Add(food, 5f);
            Assert.That(inventory.Reserve(food, 6f), Is.False);
            Assert.That(inventory.GetReserved(food), Is.EqualTo(0f));
            Assert.That(inventory.Reserve(food, 3f), Is.True);
            Assert.That(inventory.GetAvailable(food), Is.EqualTo(2f));
        }

        [Test]
        public void WithdrawReservedDecreasesOnHandAndReservation()
        {
            inventory.Add(food, 5f);
            Assert.That(inventory.Reserve(food, 3f), Is.True);
            Assert.That(inventory.WithdrawReserved(food, 2f), Is.EqualTo(2f));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(3f));
            Assert.That(inventory.GetReserved(food), Is.EqualTo(1f));
        }

        [Test]
        public void ReleaseReservationRestoresAvailability()
        {
            inventory.Add(food, 5f);
            inventory.Reserve(food, 3f);
            inventory.ReleaseReservation(food, 2f);
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(5f));
            Assert.That(inventory.GetReserved(food), Is.EqualTo(1f));
            Assert.That(inventory.GetAvailable(food), Is.EqualTo(4f));
        }

        [Test]
        public void InventoryNeverBecomesNegative()
        {
            Assert.That(inventory.Remove(food, 2f), Is.EqualTo(0f));
            inventory.Add(food, 1f);
            Assert.That(inventory.Remove(food, 2f), Is.EqualTo(1f));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(0f));
            Assert.That(inventory.GetReserved(food), Is.EqualTo(0f));
        }

        [Test]
        public void OwnedTransfersConserveStockAndKeepReservationsAllocationScoped()
        {
            inventory.Add(food, 4f);
            Assert.That(inventory.SetCapacity(food, 2f), Is.False);
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(4f));
            Assert.That(inventory.GetCapacity(food), Is.EqualTo(10f));
            Assert.That(inventory.TryReserveOwned(food, 2f, out InventoryReservationToken first), Is.True);
            Assert.That(inventory.TryReserveOwned(food, 2f, out InventoryReservationToken second), Is.True);

            destinationObject = new GameObject("Freight Destination Inventory Test");
            InventoryComponent destination = destinationObject.AddComponent<InventoryComponent>();
            Assert.That(destination.SetCapacity(food, 1f), Is.True);

            inventory.ReleaseOwned(first);
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(inventory.GetReserved(food), Is.EqualTo(2f));
            Assert.That(inventory.Reserve(food, 1f), Is.True);
            inventory.ReleaseReservation(food, 3f);
            Assert.That(inventory.GetReserved(food), Is.EqualTo(2f),
                "Legacy release APIs must not release freight-owned allocations.");

            float expectedTotal = inventory.GetOnHand(food) + destination.GetOnHand(food);
            bool observedConservedState = true;
            inventory.OnChanged += (_, resource) =>
            {
                if (resource == food)
                    observedConservedState &= Mathf.Abs(
                        inventory.GetOnHand(food) + destination.GetOnHand(food) - expectedTotal) < 0.0001f;
            };
            destination.OnChanged += (_, resource) =>
            {
                if (resource == food)
                    observedConservedState &= Mathf.Abs(
                        inventory.GetOnHand(food) + destination.GetOnHand(food) - expectedTotal) < 0.0001f;
            };

            Assert.That(inventory.TransferOwnedTo(second, destination, 2f), Is.EqualTo(1f));
            Assert.That(second.IsActive, Is.True);
            Assert.That(second.Remaining, Is.EqualTo(1f));
            Assert.That(inventory.GetOnHand(food) + destination.GetOnHand(food), Is.EqualTo(expectedTotal));
            Assert.That(observedConservedState, Is.True);

            Assert.That(destination.SetCapacity(food, 3f), Is.True);
            Assert.That(inventory.TransferOwnedTo(second, destination, 1f), Is.EqualTo(1f));
            Assert.That(second.IsActive, Is.False);
            Assert.That(inventory.GetOnHand(food) + destination.GetOnHand(food), Is.EqualTo(expectedTotal));
        }

        [Test]
        public void LegacyAndOwnedReservationsCannotAllocateTheSameStockTwice()
        {
            inventory.Add(food, 5f);

            Assert.That(inventory.TryReserveOwned(food, 4f, out InventoryReservationToken owned), Is.True);
            Assert.That(inventory.Reserve(food, 2f), Is.False);
            Assert.That(inventory.TryReserveOwned(food, 2f, out _), Is.False);
            Assert.That(inventory.GetReserved(food), Is.EqualTo(4f));
            Assert.That(inventory.GetAvailable(food), Is.EqualTo(1f));
            Assert.That(owned.IsActive, Is.True);
        }

        [Test]
        public void LegacyAndOwnedReservationsReconcileFromTheirOwners()
        {
            inventory.Add(food, 6f);
            Assert.That(inventory.Reserve(food, 2f), Is.True);
            Assert.That(inventory.TryReserveOwned(food, 3f, out InventoryReservationToken owned), Is.True);

            Assert.That(inventory.GetReserved(food), Is.EqualTo(5f));
            inventory.ReleaseReservation(food, 2f);
            Assert.That(inventory.GetReserved(food), Is.EqualTo(3f));
            Assert.That(owned.IsActive, Is.True);
            Assert.That(inventory.ReleaseOwned(owned), Is.EqualTo(3f));
            Assert.That(inventory.GetReserved(food), Is.EqualTo(0f));
        }

        [Test]
        public void ReservationAggregateIsTransientAndReloadCannotCreateOrphanReservations()
        {
            inventory.Add(food, 5f);
            Assert.That(inventory.Reserve(food, 3f), Is.True);
            InventoryEntry entry = inventory.GetEntry(food);
            entry.reserved = 3f;

            Assert.That(typeof(InventoryEntry).GetField("reserved").IsNotSerialized, Is.True);
            FieldInfo ownedField = typeof(InventoryComponent).GetField(
                "ownedReservations", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo legacyField = typeof(InventoryComponent).GetField(
                "legacyReservations", BindingFlags.Instance | BindingFlags.NonPublic);
            ownedField.SetValue(inventory, null);
            legacyField.SetValue(inventory, null);

            typeof(InventoryComponent).GetMethod(
                "OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inventory, null);

            Assert.That(inventory.GetReserved(food), Is.EqualTo(0f));
            Assert.That(inventory.GetAvailable(food), Is.EqualTo(5f));
        }

        [Test]
        public void RemovingStockTrimsNewestOwnedReservationFirst()
        {
            inventory.Add(food, 6f);
            Assert.That(inventory.TryReserveOwned(food, 2f, out InventoryReservationToken oldest), Is.True);
            Assert.That(inventory.TryReserveOwned(food, 2f, out InventoryReservationToken newest), Is.True);

            Assert.That(inventory.Remove(food, 3f), Is.EqualTo(3f));

            Assert.That(oldest.IsActive, Is.True);
            Assert.That(oldest.Remaining, Is.EqualTo(2f));
            Assert.That(newest.IsActive, Is.True);
            Assert.That(newest.Remaining, Is.EqualTo(1f));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(3f));
            Assert.That(inventory.GetReserved(food), Is.EqualTo(3f));
        }

        [Test]
        public void DiscreteInventoryRejectsFractionalRuntimeMutation()
        {
            LogAssert.Expect(
                LogType.Error,
                "Rejected invalid Discrete Test inventory quantity: 0.2.");
            Assert.That(inventory.Add(discrete, 0.2f), Is.EqualTo(0f));
            Assert.That(inventory.GetOnHand(discrete), Is.EqualTo(0f));
            Assert.That(inventory.Add(discrete, 2.00001f), Is.EqualTo(2f));
        }
    }
}
