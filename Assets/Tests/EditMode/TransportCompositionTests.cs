using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class TransportCompositionTests
    {
        private GameObject shipObject;
        private GameObject sourceObject;
        private GameObject passengerObject;
        private ResourceDefinition wrench;
        private WorkerClassDefinition pilotClass;

        [SetUp]
        public void SetUp()
        {
            shipObject = new GameObject("Transport");
            shipObject.AddComponent<LocationAnchor>();
            shipObject.AddComponent<ShipComponent>();
            shipObject.AddComponent<InventoryComponent>();
            shipObject.AddComponent<TransportVehicleComponent>();
            sourceObject = new GameObject("Source");
            sourceObject.AddComponent<LocationAnchor>();
            passengerObject = new GameObject("Passenger");
            wrench = ScriptableObject.CreateInstance<ResourceDefinition>();
            wrench.quantityMode = ResourceQuantityMode.Discrete;
            pilotClass = ScriptableObject.CreateInstance<WorkerClassDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(pilotClass);
            Object.DestroyImmediate(wrench);
            Object.DestroyImmediate(passengerObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(shipObject);
        }

        [Test]
        public void QualifiedPilotMustHaveRequiredClassAndBeAboard()
        {
            ShipComponent ship = shipObject.GetComponent<ShipComponent>();
            ColonistAgent pilot = passengerObject.AddComponent<ColonistAgent>();
            pilot.classes.Add(pilotClass);
            ship.requiredPilotClass = pilotClass;
            ship.assignedPilot = pilot;

            Assert.That(ship.HasQualifiedPilot, Is.True);
            Assert.That(ship.IsOperationallyCrewed, Is.False);

            pilot.currentLocation = shipObject.GetComponent<LocationAnchor>();

            Assert.That(ship.IsOperationallyCrewed, Is.True);
        }

        [Test]
        public void PassengerCarrierEnforcesCapacityAndPhysicalLocations()
        {
            PassengerCarrierComponent carrier = shipObject.AddComponent<PassengerCarrierComponent>();
            LocationAnchor source = sourceObject.GetComponent<LocationAnchor>();
            LocationAnchor shipLocation = shipObject.GetComponent<LocationAnchor>();
            ColonistAgent passenger = passengerObject.AddComponent<ColonistAgent>();
            passenger.currentLocation = source;

            Assert.That(carrier.TryBoardPassengers(new List<ColonistAgent> { passenger }, source), Is.True);
            Assert.That(passenger.currentLocation, Is.EqualTo(shipLocation));
            Assert.That(carrier.AboardCount, Is.EqualTo(1));
            Assert.That(carrier.TryUnboardPassengers(source), Is.True);
            Assert.That(passenger.currentLocation, Is.EqualTo(source));
        }

        [Test]
        public void DiscreteFreeCargoCapacityIsWholeUnits()
        {
            InventoryComponent inventory = shipObject.GetComponent<InventoryComponent>();
            Configure(inventory, wrench, 3.9f, 1.1f);

            float free = shipObject.GetComponent<TransportVehicleComponent>().GetFreeCargoCapacity(wrench);

            Assert.That(free, Is.EqualTo(2f));
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
