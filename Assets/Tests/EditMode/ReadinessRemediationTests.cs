using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ReadinessRemediationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = assets.Count - 1; i >= 0; i--)
                if (assets[i] != null)
                    Object.DestroyImmediate(assets[i]);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            assets.Clear();
            objects.Clear();
        }

        [Test]
        public void TransitDoesNotCommitDestinationBeforeArrival()
        {
            LocationAnchor origin = New("Origin").AddComponent<LocationAnchor>();
            LocationAnchor destination = New("Destination").AddComponent<LocationAnchor>();
            ColonistAgent colonist = New("Colonist").AddComponent<ColonistAgent>();
            colonist.currentLocation = origin;

            Assert.That(colonist.BeginTransit(origin, destination), Is.True);
            Assert.That(colonist.currentLocation, Is.EqualTo(origin));
            Assert.That(colonist.TransitDestination, Is.EqualTo(destination));
            Assert.That(colonist.transform.parent, Is.Null);

            Assert.That(colonist.CompleteTransit(), Is.True);
            Assert.That(colonist.currentLocation, Is.EqualTo(destination));
            Assert.That(colonist.CompleteTransit(), Is.False);
        }

        [Test]
        public void ShipDisableDoesNotRemoveOccupantsFromPopulation()
        {
            GameObject populationObject = New("Population");
            PopulationManager population = populationObject.AddComponent<PopulationManager>();
            GameObject shipObject = New("Ship");
            LocationAnchor shipAnchor = shipObject.AddComponent<LocationAnchor>();
            shipObject.AddComponent<ShipComponent>();
            ColonistAgent colonist = New("Occupant").AddComponent<ColonistAgent>();
            colonist.currentLocation = shipAnchor;

            shipObject.SetActive(false);

            Assert.That(population.Colonists, Does.Contain(colonist));
            Assert.That(colonist.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void OnlyMovementOwnerCanCommitArrival()
        {
            GameObject shipObject = New("Ship");
            ShipComponent ship = shipObject.AddComponent<ShipComponent>();
            LocationAnchor source = New("Source").AddComponent<LocationAnchor>();
            LocationAnchor destination = New("Destination").AddComponent<LocationAnchor>();
            ship.SetDock(source);

            Assert.That(ship.TryClaimMovement(ShipMovementOwner.Transport), Is.True);
            ship.BeginDocking(destination);
            Assert.That(ship.TryCompleteArrival(ShipMovementOwner.Extraction, destination), Is.False);
            Assert.That(ship.CurrentDock, Is.EqualTo(source));
            Assert.That(ship.TryCompleteArrival(ShipMovementOwner.Transport, destination), Is.True);
            Assert.That(ship.CurrentDock, Is.EqualTo(destination));
        }

        [Test]
        public void MissingMovementBlocksTransportExecutor()
        {
            GameObject vehicleObject = New("Transport");
            LocationAnchor vehicleLocation = vehicleObject.AddComponent<LocationAnchor>();
            ShipComponent ship = vehicleObject.AddComponent<ShipComponent>();
            StaffingComponent roster = vehicleObject.AddComponent<StaffingComponent>();
            roster.workplaceLocation = vehicleLocation;
            WorkerClassDefinition pilotClass = Asset<WorkerClassDefinition>();
            StaffingRoleDefinition pilotRole = Asset<StaffingRoleDefinition>();
            pilotRole.requiredClass = pilotClass;
            pilotRole.maximumAssignedPerShift = 1;
            ship.operatingRole = pilotRole;
            ship.crewChangeBase = vehicleLocation;
            ship.initialDock = vehicleLocation;
            roster.offeredRoles.Add(pilotRole);
            ship.crewStaffing = roster;
            ship.SetDock(vehicleLocation);
            ColonistAgent pilot = New("Pilot").AddComponent<ColonistAgent>();
            pilot.home = vehicleLocation;
            pilot.currentLocation = vehicleLocation;
            pilot.classes.Add(pilotClass);
            ship.AdoptResponsiblePilot(pilot);
            pilot.Status.BeginPilotDuty(ship, pilotRole, "A", 0f);
            vehicleObject.AddComponent<ShipCrewDutyComponent>();
            TransportExecutorComponent executor = vehicleObject.AddComponent<TransportExecutorComponent>();
            TransportVehicleComponent vehicle = vehicleObject.AddComponent<TransportVehicleComponent>();
            LocationAnchor pickup = New("Pickup").AddComponent<LocationAnchor>();
            TransportContract contract = new TransportContract
            {
                contractId = 77,
                type = TransportContractType.Passenger,
                sourceLocation = pickup,
                destinationLocation = vehicleLocation,
                state = TransportContractState.Open
            };

            Assert.That(vehicle.StartContract(contract), Is.True);
            executor.SimulationTick(1f);

            Assert.That(executor.State, Is.EqualTo(TransportExecutionState.Blocked));
            Assert.That(contract.state, Is.EqualTo(TransportContractState.TravelingToPickup));
        }

        [Test]
        public void DisabledWorkerStillCountsTowardCapacity()
        {
            StaffingManager manager = New("Staffing").AddComponent<StaffingManager>();
            LocationAnchor workplaceAnchor = New("Workplace").AddComponent<LocationAnchor>();
            StaffingComponent workplace = workplaceAnchor.gameObject.AddComponent<StaffingComponent>();
            workplace.workplaceLocation = workplaceAnchor;
            workplace.shiftPattern = Asset<ShiftPatternDefinition>();
            workplace.shiftPattern.shifts.Add(new ShiftDefinition { shiftId = "A", durationHours = 8f });
            WorkerClassDefinition workerClass = Asset<WorkerClassDefinition>();
            StaffingRoleDefinition role = Asset<StaffingRoleDefinition>();
            role.requiredClass = workerClass;
            role.maximumAssignedPerShift = 1;
            role.minimumActiveForOperation = 1;
            workplace.offeredRoles.Add(role);

            ColonistAgent first = New("First").AddComponent<ColonistAgent>();
            first.classes.Add(workerClass);
            ColonistAgent second = New("Second").AddComponent<ColonistAgent>();
            second.classes.Add(workerClass);

            Assert.That(manager.Assign(first, workplace, role, "A"), Is.EqualTo(AssignmentResult.Applied));
            first.gameObject.SetActive(false);

            Assert.That(manager.ValidateAssignment(second, workplace, role, "A"),
                Is.EqualTo(AssignmentResult.RejectedAtCapacity));
        }

        [Test]
        public void DisabledStaffingProviderBlocksFacilityAndReleasesWorker()
        {
            StaffingManager manager = New("Staffing").AddComponent<StaffingManager>();
            LocationAnchor facilityAnchor = New("Facility").AddComponent<LocationAnchor>();
            StaffingComponent staffing = facilityAnchor.gameObject.AddComponent<StaffingComponent>();
            staffing.workplaceLocation = facilityAnchor;
            staffing.shiftPattern = Asset<ShiftPatternDefinition>();
            staffing.shiftPattern.shifts.Add(new ShiftDefinition { shiftId = "A", durationHours = 8f });
            WorkerClassDefinition workerClass = Asset<WorkerClassDefinition>();
            StaffingRoleDefinition role = Asset<StaffingRoleDefinition>();
            role.requiredClass = workerClass;
            role.maximumAssignedPerShift = 1;
            role.minimumActiveForOperation = 1;
            staffing.offeredRoles.Add(role);
            ColonistAgent worker = New("Worker").AddComponent<ColonistAgent>();
            worker.home = facilityAnchor;
            worker.currentLocation = facilityAnchor;
            worker.classes.Add(workerClass);
            FacilityPerformanceComponent performance = facilityAnchor.gameObject.AddComponent<FacilityPerformanceComponent>();

            Assert.That(manager.Assign(worker, staffing, role, "A"), Is.EqualTo(AssignmentResult.Applied));
            performance.EvaluateAt(0f);
            Assert.That(performance.IsOperational, Is.True);

            staffing.enabled = false;
            performance.EvaluateAt(0f);
            manager.SimulationTick(0.1f);
            Assert.That(performance.IsOperational, Is.False);
            Assert.That(performance.BlockSummary, Does.Contain("staffing"));
            Assert.That(worker.activity, Is.Not.EqualTo(ColonistActivity.Working));

            staffing.enabled = true;
            manager.SimulationTick(0.1f);
            Assert.That(performance.IsOperational, Is.True);
        }

        [Test]
        public void InventoryRackViewRebuildDoesNotMutateInventory()
        {
            GameObject inventoryObject = New("Inventory");
            InventoryComponent inventory = inventoryObject.AddComponent<InventoryComponent>();
            ResourceDefinition resource = Asset<ResourceDefinition>();
            resource.displayName = "Food";
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry { resource = resource, capacity = 10f, onHand = 4f });
            InventoryRackView view = inventoryObject.AddComponent<InventoryRackView>();

            view.Refresh();
            Assert.That(view.VisibleCargo, Has.Count.EqualTo(1));
            Object.DestroyImmediate(view);
            Assert.That(inventory.GetOnHand(resource), Is.EqualTo(4f));
        }

        private GameObject New(string objectName)
        {
            GameObject created = new GameObject(objectName);
            objects.Add(created);
            return created;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
