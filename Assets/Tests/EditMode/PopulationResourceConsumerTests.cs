using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class PopulationResourceConsumerTests
    {
        private GameObject populationObject;
        private GameObject habitationObject;
        private GameObject colonistOne;
        private GameObject colonistTwo;
        private ResourceDefinition water;

        [SetUp]
        public void SetUp()
        {
            populationObject = new GameObject("Population");
            PopulationManager population = populationObject.AddComponent<PopulationManager>();

            habitationObject = new GameObject("Habitation");
            LocationAnchor location = habitationObject.AddComponent<LocationAnchor>();
            location.displayName = "Habitation Test";
            HabitationComponent habitation = habitationObject.AddComponent<HabitationComponent>();
            habitation.location = location;

            colonistOne = CreateResident(population, location, "Resident One");
            colonistTwo = CreateResident(population, location, "Resident Two");

            water = ScriptableObject.CreateInstance<ResourceDefinition>();
            water.name = "Water Test";
            water.quantityMode = ResourceQuantityMode.Fractional;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(water);
            Object.DestroyImmediate(colonistTwo);
            Object.DestroyImmediate(colonistOne);
            Object.DestroyImmediate(habitationObject);
            Object.DestroyImmediate(populationObject);
        }

        [Test]
        public void ResidentsConsumeConfiguredRateAndReportShortage()
        {
            InventoryComponent inventory = habitationObject.AddComponent<InventoryComponent>();
            Configure(inventory, water, 10f, 1f);
            PopulationResourceConsumer consumer = habitationObject.AddComponent<PopulationResourceConsumer>();
            consumer.habitation = habitationObject.GetComponent<HabitationComponent>();
            consumer.inventory = inventory;
            consumer.entries.Add(new PopulationConsumptionEntry
            {
                resource = water,
                amountPerResidentPerGameHour = 0.25f
            });

            consumer.SimulationTick(1f);
            PopulationConsumptionEntry entry = consumer.entries[0];

            Assert.That(consumer.ResidentCount, Is.EqualTo(2));
            Assert.That(entry.RequestedLastTick, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(entry.ConsumedLastTick, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(entry.ShortageActive, Is.False);

            consumer.SimulationTick(2f);

            Assert.That(entry.RequestedLastTick, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(entry.ConsumedLastTick, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(entry.ShortageActive, Is.True);
        }

        [Test]
        public void DiscreteConsumptionRateIsRejected()
        {
            water.quantityMode = ResourceQuantityMode.Discrete;
            PopulationConsumptionEntry entry = new PopulationConsumptionEntry
            {
                resource = water,
                amountPerResidentPerGameHour = 0.25f
            };

            Assert.That(entry.Validate(), Is.False);
        }

        private static GameObject CreateResident(PopulationManager population, LocationAnchor home, string displayName)
        {
            GameObject residentObject = new GameObject(displayName);
            ColonistAgent resident = residentObject.AddComponent<ColonistAgent>();
            resident.displayName = displayName;
            resident.home = home;
            population.Register(resident);
            return residentObject;
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
