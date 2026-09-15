using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class DiscreteFreightTests
    {
        private GameObject managerObject;
        private GameObject sourceObject;
        private GameObject destinationObject;
        private ResourceDefinition wrench;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Contract Manager Test");
            managerObject.AddComponent<ContractManager>();
            sourceObject = new GameObject("Source");
            destinationObject = new GameObject("Destination");
            wrench = ScriptableObject.CreateInstance<ResourceDefinition>();
            wrench.name = "Wrench Test";
            wrench.quantityMode = ResourceQuantityMode.Discrete;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(wrench);
            Object.DestroyImmediate(destinationObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void DiscreteFreightRejectsFractionalContract()
        {
            LocationAnchor source = sourceObject.AddComponent<LocationAnchor>();
            LocationAnchor destination = destinationObject.AddComponent<LocationAnchor>();
            InventoryComponent sourceInventory = sourceObject.AddComponent<InventoryComponent>();
            InventoryComponent destinationInventory = destinationObject.AddComponent<InventoryComponent>();
            Configure(sourceInventory, wrench, 10f, 5f);
            Configure(destinationInventory, wrench, 10f, 0f);

            TransportContract contract = ContractManager.Instance.CreateFreightContract(
                wrench, 0.2f, sourceInventory, destinationInventory, source, destination, 5);

            Assert.That(contract, Is.Null);
            Assert.That(sourceInventory.GetReserved(wrench), Is.EqualTo(0f));
        }

        private static void Configure(InventoryComponent inventory, ResourceDefinition resource,
            float capacity, float onHand)
        {
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry { resource = resource, capacity = capacity, onHand = onHand });
        }
    }
}
