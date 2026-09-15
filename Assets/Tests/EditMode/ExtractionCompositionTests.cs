using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ExtractionCompositionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private ResourceDefinition ice;
        private ResourceDefinition wrench;

        [SetUp]
        public void SetUp()
        {
            ice = ScriptableObject.CreateInstance<ResourceDefinition>();
            ice.quantityMode = ResourceQuantityMode.Fractional;
            wrench = ScriptableObject.CreateInstance<ResourceDefinition>();
            wrench.quantityMode = ResourceQuantityMode.Discrete;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(wrench);
            Object.DestroyImmediate(ice);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void CollectorPreservesFractionalFinalDepositQuantity()
        {
            ResourceDeposit deposit = CreateDeposit(ice, 1.5f);
            InventoryComponent cargo = CreateCargo(ice, 10f, 0f);
            ResourceCollectorComponent collector = cargo.gameObject.AddComponent<ResourceCollectorComponent>();
            collector.collectableResource = ice;
            collector.extractionRatePerGameHour = 6f;
            collector.destinationCargo = cargo;

            Assert.That(collector.Collect(deposit, 1f), Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(deposit.RemainingQuantity, Is.EqualTo(0f));
            Assert.That(cargo.GetOnHand(ice), Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void CollectorRejectsWrongResource()
        {
            ResourceDeposit deposit = CreateDeposit(ice, 5f);
            InventoryComponent cargo = CreateCargo(wrench, 5f, 0f);
            ResourceCollectorComponent collector = cargo.gameObject.AddComponent<ResourceCollectorComponent>();
            collector.collectableResource = wrench;
            collector.extractionRatePerGameHour = 1f;
            collector.destinationCargo = cargo;

            Assert.That(collector.Collect(deposit, 1f), Is.EqualTo(0f));
            Assert.That(deposit.RemainingQuantity, Is.EqualTo(5f));
        }

        [Test]
        public void DiscreteCollectorAccumulatesWholeUnitWork()
        {
            ResourceDeposit deposit = CreateDeposit(wrench, 3f);
            InventoryComponent cargo = CreateCargo(wrench, 5f, 0f);
            ResourceCollectorComponent collector = cargo.gameObject.AddComponent<ResourceCollectorComponent>();
            collector.collectableResource = wrench;
            collector.extractionRatePerGameHour = 0.2f;
            collector.destinationCargo = cargo;

            Assert.That(collector.Collect(deposit, 1f), Is.EqualTo(0f));
            Assert.That(collector.Collect(deposit, 4f), Is.EqualTo(1f));
            Assert.That(cargo.GetOnHand(wrench) % 1f, Is.EqualTo(0f));
            Assert.That(deposit.RemainingQuantity, Is.EqualTo(2f));
        }

        [Test]
        public void FullDestinationPreventsExtractionMissionLaunch()
        {
            GameObject shipObject = CreateObject("Mining Ship");
            LocationAnchor shipLocation = shipObject.AddComponent<LocationAnchor>();
            ShipComponent ship = shipObject.AddComponent<ShipComponent>();
            ColonistAgent pilot = CreateObject("Pilot").AddComponent<ColonistAgent>();
            WorkerClassDefinition pilotClass = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            pilot.classes.Add(pilotClass);
            ship.requiredPilotClass = pilotClass;
            ship.assignedPilot = pilot;
            pilot.currentLocation = shipLocation;
            InventoryComponent cargo = shipObject.AddComponent<InventoryComponent>();
            Configure(cargo, ice, 10f, 0f);
            ShipMovementComponent movement = shipObject.AddComponent<ShipMovementComponent>();
            ResourceCollectorComponent collector = shipObject.AddComponent<ResourceCollectorComponent>();
            collector.collectableResource = ice;
            collector.destinationCargo = cargo;
            ExtractionMissionController mission = shipObject.AddComponent<ExtractionMissionController>();
            GameObject destinationObject = CreateObject("Full Destination");
            LocationAnchor destination = destinationObject.AddComponent<LocationAnchor>();
            InventoryComponent destinationInventory = destinationObject.AddComponent<InventoryComponent>();
            Configure(destinationInventory, ice, 10f, 10f);
            mission.ship = ship;
            mission.movement = movement;
            mission.collector = collector;
            mission.targetDeposit = CreateDeposit(ice, 10f);
            mission.unloadLocation = destination;
            mission.unloadInventory = destinationInventory;
            mission.fallbackTargetStock = 10f;

            mission.SimulationTick(1f);

            Assert.That(mission.State, Is.EqualTo(ExtractionMissionState.Idle));
            Assert.That(mission.targetDeposit.RemainingQuantity, Is.EqualTo(10f));
            Object.DestroyImmediate(pilotClass);
        }

        [Test]
        public void UnloadBlockageKeepsCargoOnShip()
        {
            GameObject shipObject = CreateObject("Mining Ship");
            InventoryComponent cargo = shipObject.AddComponent<InventoryComponent>();
            Configure(cargo, ice, 10f, 5f);
            ResourceCollectorComponent collector = shipObject.AddComponent<ResourceCollectorComponent>();
            collector.collectableResource = ice;
            collector.destinationCargo = cargo;
            ExtractionMissionController mission = shipObject.AddComponent<ExtractionMissionController>();
            GameObject destinationObject = CreateObject("Blocked Destination");
            LocationAnchor destination = destinationObject.AddComponent<LocationAnchor>();
            InventoryComponent destinationInventory = destinationObject.AddComponent<InventoryComponent>();
            Configure(destinationInventory, ice, 10f, 7f);
            mission.collector = collector;
            mission.unloadLocation = destination;
            mission.unloadInventory = destinationInventory;
            FieldInfo state = typeof(ExtractionMissionController).GetField(
                "state", BindingFlags.Instance | BindingFlags.NonPublic);
            state.SetValue(mission, ExtractionMissionState.Unloading);

            mission.SimulationTick(1f);

            Assert.That(cargo.GetOnHand(ice), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(destinationInventory.GetOnHand(ice), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(mission.State, Is.EqualTo(ExtractionMissionState.Unloading));
        }

        private ResourceDeposit CreateDeposit(ResourceDefinition resource, float quantity)
        {
            GameObject depositObject = CreateObject("Deposit");
            ResourceDeposit deposit = depositObject.AddComponent<ResourceDeposit>();
            deposit.resource = resource;
            deposit.startingQuantity = quantity;
            FieldInfo remaining = typeof(ResourceDeposit).GetField(
                "remainingQuantity", BindingFlags.Instance | BindingFlags.NonPublic);
            remaining.SetValue(deposit, quantity);
            return deposit;
        }

        private InventoryComponent CreateCargo(ResourceDefinition resource, float capacity, float onHand)
        {
            GameObject cargoObject = CreateObject("Cargo");
            InventoryComponent cargo = cargoObject.AddComponent<InventoryComponent>();
            Configure(cargo, resource, capacity, onHand);
            return cargo;
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject created = new GameObject(objectName);
            objects.Add(created);
            return created;
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
