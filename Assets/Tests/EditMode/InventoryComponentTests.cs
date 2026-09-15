using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class InventoryComponentTests
    {
        private GameObject inventoryObject;
        private InventoryComponent inventory;

        [SetUp]
        public void SetUp()
        {
            inventoryObject = new GameObject("Inventory Test");
            inventory = inventoryObject.AddComponent<InventoryComponent>();
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry
            {
                resource = ResourceType.Food,
                capacity = 10f
            });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(inventoryObject);
        }

        [Test]
        public void AddRespectsCapacity()
        {
            Assert.That(inventory.Add(ResourceType.Food, 12f), Is.EqualTo(10f));
            Assert.That(inventory.GetOnHand(ResourceType.Food), Is.EqualTo(10f));
            Assert.That(inventory.GetAvailable(ResourceType.Food), Is.EqualTo(10f));
        }

        [Test]
        public void ReserveCannotExceedAvailable()
        {
            inventory.Add(ResourceType.Food, 5f);
            Assert.That(inventory.Reserve(ResourceType.Food, 6f), Is.False);
            Assert.That(inventory.GetReserved(ResourceType.Food), Is.EqualTo(0f));
            Assert.That(inventory.Reserve(ResourceType.Food, 3f), Is.True);
            Assert.That(inventory.GetAvailable(ResourceType.Food), Is.EqualTo(2f));
        }

        [Test]
        public void WithdrawReservedDecreasesOnHandAndReservation()
        {
            inventory.Add(ResourceType.Food, 5f);
            Assert.That(inventory.Reserve(ResourceType.Food, 3f), Is.True);
            Assert.That(inventory.WithdrawReserved(ResourceType.Food, 2f), Is.EqualTo(2f));
            Assert.That(inventory.GetOnHand(ResourceType.Food), Is.EqualTo(3f));
            Assert.That(inventory.GetReserved(ResourceType.Food), Is.EqualTo(1f));
        }

        [Test]
        public void ReleaseReservationRestoresAvailability()
        {
            inventory.Add(ResourceType.Food, 5f);
            inventory.Reserve(ResourceType.Food, 3f);
            inventory.ReleaseReservation(ResourceType.Food, 2f);
            Assert.That(inventory.GetOnHand(ResourceType.Food), Is.EqualTo(5f));
            Assert.That(inventory.GetReserved(ResourceType.Food), Is.EqualTo(1f));
            Assert.That(inventory.GetAvailable(ResourceType.Food), Is.EqualTo(4f));
        }

        [Test]
        public void InventoryNeverBecomesNegative()
        {
            Assert.That(inventory.Remove(ResourceType.Food, 2f), Is.EqualTo(0f));
            inventory.Add(ResourceType.Food, 1f);
            Assert.That(inventory.Remove(ResourceType.Food, 2f), Is.EqualTo(1f));
            Assert.That(inventory.GetOnHand(ResourceType.Food), Is.EqualTo(0f));
            Assert.That(inventory.GetReserved(ResourceType.Food), Is.EqualTo(0f));
        }
    }
}
