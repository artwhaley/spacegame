using System.Collections.Generic;
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
        public void DefaultContractIsNotLiveWork()
        {
            TransportContract contract = new TransportContract
            {
                state = TransportContractState.Open
            };

            Assert.That(contract.IsActive, Is.False);
            Assert.That(contract.IsAssignedOrInFlight, Is.False);
        }

        [Test]
        public void AssignedTransportAdvancesTowardPickupOnSimulationTick()
        {
            GameObject clockObject = CreateObject("Simulation");
            clockObject.AddComponent<SimulationManager>();

            GameObject vehicleObject = CreateObject("Shuttle");
            LocationAnchor vehicleLocation = vehicleObject.AddComponent<LocationAnchor>();
            ShipComponent ship = vehicleObject.AddComponent<ShipComponent>();
            StaffingComponent roster = vehicleObject.AddComponent<StaffingComponent>();
            roster.workplaceLocation = vehicleLocation;
            WorkerClassDefinition pilotClass = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            StaffingRoleDefinition pilotRole = ScriptableObject.CreateInstance<StaffingRoleDefinition>();
            pilotRole.requiredClass = pilotClass;
            ColonistAgent pilot = CreateObject("Pilot").AddComponent<ColonistAgent>();
            pilot.classes.Add(pilotClass);
            pilot.currentLocation = vehicleLocation;
            ship.operatingRole = pilotRole;
            ship.crewChangeBase = vehicleLocation;
            ship.initialDock = vehicleLocation;
            pilot.home = vehicleLocation;
            roster.offeredRoles.Add(pilotRole);
            ship.crewStaffing = roster;
            ship.SetDock(vehicleLocation);
            ship.AdoptResponsiblePilot(pilot);
            pilot.Status.BeginPilotDuty(ship, pilotRole, "A", 0f);
            vehicleObject.AddComponent<ShipCrewDutyComponent>();
            vehicleObject.AddComponent<ShipMovementComponent>();
            TransportExecutorComponent executor = vehicleObject.AddComponent<TransportExecutorComponent>();
            TransportVehicleComponent vehicle = vehicleObject.AddComponent<TransportVehicleComponent>();

            LocationAnchor pickup = CreateObject("Pickup").AddComponent<LocationAnchor>();
            pickup.transform.position = new Vector3(10f, 0f, 0f);
            TransportContract contract = new TransportContract
            {
                contractId = 1,
                type = TransportContractType.Passenger,
                sourceLocation = pickup,
                destinationLocation = vehicleLocation,
                state = TransportContractState.Open
            };

            Assert.That(vehicle.StartContract(contract), Is.True);

            float positionBeforeTick = vehicleObject.transform.position.x;
            // Exercise the public tick contract. A tenth of a game hour matches
            // the default SimulationManager interval without reaching pickup.
            executor.SimulationTick(0.1f);

            Assert.That(vehicleObject.transform.position.x, Is.GreaterThan(positionBeforeTick));
            Assert.That(executor.State, Is.EqualTo(TransportExecutionState.TravelingToPickup));

            Object.DestroyImmediate(pilotRole);
            Object.DestroyImmediate(pilotClass);
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject created = new GameObject(objectName);
            objects.Add(created);
            return created;
        }

    }
}
