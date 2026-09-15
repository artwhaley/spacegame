using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class UnifiedDispatchTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private GameObject contractManagerObject;
        private GameObject logisticsManagerObject;
        private ResourceDefinition food;
        private WorkerClassDefinition pilotClass;

        private LocationAnchor source;
        private InventoryComponent sourceInventory;
        private LocationAnchor destination;
        private InventoryComponent destinationInventory;

        [SetUp]
        public void SetUp()
        {
            contractManagerObject = CreateObject("Contracts");
            contractManagerObject.AddComponent<ContractManager>();
            logisticsManagerObject = CreateObject("Logistics");
            logisticsManagerObject.AddComponent<LogisticsManager>();

            food = ScriptableObject.CreateInstance<ResourceDefinition>();
            food.displayName = "Food";
            food.quantityMode = ResourceQuantityMode.Fractional;
            pilotClass = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            pilotClass.displayName = "Pilot";

            source = CreateObject("Source").AddComponent<LocationAnchor>();
            source.displayName = "Source";
            sourceInventory = source.gameObject.AddComponent<InventoryComponent>();
            Configure(sourceInventory, food, 100f, 50f);
            destination = CreateObject("Destination").AddComponent<LocationAnchor>();
            destination.displayName = "Destination";
            destinationInventory = destination.gameObject.AddComponent<InventoryComponent>();
            Configure(destinationInventory, food, 100f, 0f);
            LogisticsManager.Instance.RegisterFreightSupply(
                "Source Food", food, source, sourceInventory);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(food);
            Object.DestroyImmediate(pilotClass);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void PriorityTenPassengerBeatsPriorityNineFreight()
        {
            TransportContract passenger = CreatePassenger(10);
            CreateFreight(9, FreightDemandClass.Foreground);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.Neutral);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract, Is.EqualTo(passenger));
        }

        [Test]
        public void PriorityTenFreightBeatsPriorityNinePassenger()
        {
            CreatePassenger(9);
            FreightDemand freight = CreateFreight(10, FreightDemandClass.Foreground);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.Neutral);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract.demandId, Is.EqualTo(freight.demandId));
        }

        [Test]
        public void EqualPriorityPreferFreightWins()
        {
            CreatePassenger(5);
            CreateFreight(5, FreightDemandClass.Foreground);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.PreferFreight);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract.type, Is.EqualTo(TransportContractType.Freight));
        }

        [Test]
        public void EqualPriorityPreferPersonnelWins()
        {
            TransportContract passenger = CreatePassenger(5);
            CreateFreight(5, FreightDemandClass.Foreground);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.PreferPersonnel);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract, Is.EqualTo(passenger));
        }

        [Test]
        public void PreferenceDoesNotOverridePriority()
        {
            CreateFreight(2, FreightDemandClass.Foreground);
            TransportContract passenger = CreatePassenger(9);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.PreferFreight);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract, Is.EqualTo(passenger));
        }

        [Test]
        public void DispositionHardFiltersCategory()
        {
            TransportContract passenger = CreatePassenger(10);
            CreateFreight(10, FreightDemandClass.Foreground);
            TransportVehicleComponent personnelOnly = CreateVehicle(TransportDisposition.PersonnelOnly);
            LogisticsManager.Instance.TryAssignNext();
            Assert.That(personnelOnly.GetComponent<TransportExecutorComponent>().CurrentContract, Is.EqualTo(passenger));

            TransportVehicleComponent freightOnly = CreateVehicle(TransportDisposition.FreightOnly);
            LogisticsManager.Instance.TryAssignNext();
            Assert.That(freightOnly.GetComponent<TransportExecutorComponent>().CurrentContract.type, Is.EqualTo(TransportContractType.Freight));
        }

        [Test]
        public void ForegroundBeatsBackgroundRegardlessOfPriority()
        {
            CreateFreight(1, FreightDemandClass.Foreground);
            FreightDemand background = CreateFreight(10, FreightDemandClass.Background);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.Neutral);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract.demandId, Is.Not.EqualTo(background.demandId));
        }

        [Test]
        public void OldestEqualCandidateWins()
        {
            FreightDemand oldest = CreateFreight(5, FreightDemandClass.Foreground);
            FreightDemand newest = CreateFreight(5, FreightDemandClass.Foreground);
            oldest.Update(5f, 1L);
            newest.Update(5f, 2L);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.Neutral);

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract.demandId, Is.EqualTo(oldest.demandId));
        }

        [Test]
        public void DiscreteFreightIsWholeUnits()
        {
            ResourceDefinition wrench = ScriptableObject.CreateInstance<ResourceDefinition>();
            wrench.quantityMode = ResourceQuantityMode.Discrete;
            GameObject discreteSourceObject = CreateObject("Discrete Source");
            LocationAnchor discreteSource = discreteSourceObject.AddComponent<LocationAnchor>();
            InventoryComponent discreteSourceInventory = discreteSourceObject.AddComponent<InventoryComponent>();
            Configure(discreteSourceInventory, wrench, 10f, 10f);
            GameObject discreteDestinationObject = CreateObject("Discrete Destination");
            LocationAnchor discreteDestination = discreteDestinationObject.AddComponent<LocationAnchor>();
            InventoryComponent discreteDestinationInventory = discreteDestinationObject.AddComponent<InventoryComponent>();
            Configure(discreteDestinationInventory, wrench, 10f, 0f);
            LogisticsManager.Instance.RegisterFreightSupply(
                "Discrete Source", wrench, discreteSource, discreteSourceInventory);
            int demandId = LogisticsManager.Instance.RegisterFreightDemand(
                "Discrete", wrench, discreteDestination, discreteDestinationInventory, 5, 0f, 0f);
            LogisticsManager.Instance.UpdateFreightDemand(demandId, 5.7f);
            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.FreightOnly);
            Configure(vehicle.cargoInventory, wrench, 10f, 0f);

            LogisticsManager.Instance.TryAssignNext();

            TransportContract contract = vehicle.GetComponent<TransportExecutorComponent>().CurrentContract;
            Assert.That(contract.quantity, Is.EqualTo(5f));
            Assert.That(contract.quantity % 1f, Is.EqualTo(0f));
        }

        [Test]
        public void FreightReservationWaitsUntilVehicleWins()
        {
            CreateFreight(5, FreightDemandClass.Foreground);
            int sourceReservedBeforeDispatch = Mathf.RoundToInt(sourceInventory.GetReserved(food));

            LogisticsManager.Instance.TryAssignNext();

            Assert.That(sourceReservedBeforeDispatch, Is.EqualTo(0));
            Assert.That(sourceInventory.GetReserved(food), Is.EqualTo(0f));

            TransportVehicleComponent vehicle = CreateVehicle(TransportDisposition.FreightOnly);
            LogisticsManager.Instance.TryAssignNext();

            Assert.That(vehicle.GetComponent<TransportExecutorComponent>().CurrentContract, Is.Not.Null);
            Assert.That(sourceInventory.GetReserved(food), Is.GreaterThan(0f));
        }

        [Test]
        public void ChangingPriorityAffectsNextAssignmentOnly()
        {
            FreightDemand first = CreateFreight(2, FreightDemandClass.Foreground);
            FreightDemand second = CreateFreight(1, FreightDemandClass.Foreground);
            TransportVehicleComponent firstVehicle = CreateVehicle(TransportDisposition.FreightOnly);
            LogisticsManager.Instance.TryAssignNext();
            TransportContract inFlight = firstVehicle.GetComponent<TransportExecutorComponent>().CurrentContract;

            LogisticsManager.Instance.UpdateFreightDemandPolicy(second.demandId, 10, 0f, 0f, FreightDemandClass.Foreground);
            TransportVehicleComponent secondVehicle = CreateVehicle(TransportDisposition.FreightOnly);
            LogisticsManager.Instance.TryAssignNext();

            Assert.That(inFlight.demandId, Is.EqualTo(first.demandId));
            Assert.That(secondVehicle.GetComponent<TransportExecutorComponent>().CurrentContract.demandId, Is.EqualTo(second.demandId));
        }

        private TransportContract CreatePassenger(int priority)
        {
            GameObject passengerObject = CreateObject("Passenger");
            ColonistAgent passenger = passengerObject.AddComponent<ColonistAgent>();
            passenger.currentLocation = source;
            return ContractManager.Instance.CreatePassengerContract(
                source, destination, new List<ColonistAgent> { passenger }, priority);
        }

        private FreightDemand CreateFreight(int priority, FreightDemandClass demandClass)
        {
            int demandId = LogisticsManager.Instance.RegisterFreightDemand(
                "Freight", food, destination, destinationInventory, priority, 0f, 0f, demandClass);
            LogisticsManager.Instance.UpdateFreightDemand(demandId, 5f);
            for (int i = 0; i < LogisticsManager.Instance.Demands.Count; i++)
                if (LogisticsManager.Instance.Demands[i].demandId == demandId)
                    return LogisticsManager.Instance.Demands[i];
            return null;
        }

        private TransportVehicleComponent CreateVehicle(TransportDisposition disposition)
        {
            GameObject vehicleObject = CreateObject("Vehicle");
            LocationAnchor vehicleLocation = vehicleObject.AddComponent<LocationAnchor>();
            ShipComponent ship = vehicleObject.AddComponent<ShipComponent>();
            ship.requiredPilotClass = pilotClass;
            ship.assignedPilot = CreateObject("Pilot").AddComponent<ColonistAgent>();
            ship.assignedPilot.classes.Add(pilotClass);
            ship.assignedPilot.currentLocation = vehicleLocation;
            InventoryComponent cargo = vehicleObject.AddComponent<InventoryComponent>();
            Configure(cargo, food, 12f, 0f);
            vehicleObject.AddComponent<PassengerCarrierComponent>();
            vehicleObject.AddComponent<ShipMovementComponent>();
            vehicleObject.AddComponent<TransportExecutorComponent>();
            TransportVehicleComponent vehicle = vehicleObject.AddComponent<TransportVehicleComponent>();
            vehicle.disposition = disposition;
            LogisticsManager.Instance.RegisterTransportVehicle(vehicle);
            return vehicle;
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
