using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class TransportExecutorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void AssignedTransportAdvancesTowardPickupOnSimulationTick()
        {
            GameObject clockObject = CreateObject("Simulation");
            SimulationManager clock = clockObject.AddComponent<SimulationManager>();
            InvokePrivate(clock, "Awake");

            GameObject vehicleObject = CreateObject("Shuttle");
            LocationAnchor vehicleLocation = vehicleObject.AddComponent<LocationAnchor>();
            ShipComponent ship = vehicleObject.AddComponent<ShipComponent>();
            ship.assignedPilot = CreateObject("Pilot").AddComponent<ColonistAgent>();
            ship.assignedPilot.currentLocation = vehicleLocation;
            vehicleObject.AddComponent<ShipMovementComponent>();
            TransportExecutorComponent executor = vehicleObject.AddComponent<TransportExecutorComponent>();
            TransportVehicleComponent vehicle = vehicleObject.AddComponent<TransportVehicleComponent>();

            LocationAnchor pickup = CreateObject("Pickup").AddComponent<LocationAnchor>();
            pickup.transform.position = new Vector3(10f, 0f, 0f);
            TransportContract contract = new TransportContract
            {
                type = TransportContractType.Passenger,
                sourceLocation = pickup,
                destinationLocation = vehicleLocation,
                state = TransportContractState.Open
            };

            InvokePrivate(executor, "Awake");
            InvokePrivate(executor, "Start");
            Assert.That(vehicle.StartContract(contract), Is.True);

            float positionBeforeTick = vehicleObject.transform.position.x;
            InvokePrivate(clock, "AdvanceTick");

            Assert.That(vehicleObject.transform.position.x, Is.GreaterThan(positionBeforeTick));
            Assert.That(executor.State, Is.EqualTo(TransportExecutionState.TravelingToPickup));
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject created = new GameObject(objectName);
            objects.Add(created);
            return created;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }
    }
}
