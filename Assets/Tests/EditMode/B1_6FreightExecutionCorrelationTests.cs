using System.Collections.Generic;
using System.Reflection;
using AsteroidColony;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class B1_6FreightExecutionCorrelationTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private ResourceDefinition resource;

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
                if (sceneObjects[index] != null)
                    Object.DestroyImmediate(sceneObjects[index]);
            sceneObjects.Clear();
            if (resource != null)
                Object.DestroyImmediate(resource);
        }

        [Test]
        public void StaleLegOrRouteCallbacksCannotCancelCurrentFreightExecution()
        {
            LogisticsStockComponent source = CreateStockWithInventory("Source", out InventoryComponent inventory);
            LogisticsStockComponent destination = CreateStockWithInventory("Destination", out _);
            resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.name = "Correlation test resource";
            Assert.That(inventory.SetCapacity(resource, 1f), Is.True);
            Assert.That(inventory.Add(resource, 1f), Is.EqualTo(1f));
            Assert.That(inventory.TryReserveOwned(resource, 1f, out InventoryReservationToken token), Is.True);

            var managerObject = new GameObject("Freight logistics test manager");
            sceneObjects.Add(managerObject);
            var manager = managerObject.AddComponent<FreightLogisticsManager>();
            object managerAuthority = typeof(FreightLogisticsManager).GetField(
                "jobMutationAuthority", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
            var order = new FreightOrder("freight-order-test", destination, resource, 1f, 0L);
            var routePlan = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, destination, 1f)
            });
            var allocation = new FreightAllocation("freight-allocation-test", order, source, 1f, routePlan);
            var execution = new TestExecution("freight-execution-1");
            var job = new FreightDeliveryJob(allocation, execution, token, managerAuthority);
            var jobs = (List<FreightDeliveryJob>)manager.Jobs;
            jobs.Add(job);

            var started = new FreightExecutionCorrelation(
                allocation.Id, 0, execution.ExecutionId, "personnel-route-1", "stable-route-A");
            Assert.That(manager.ReportExecution(job, execution, started,
                FreightExecutionReport.PickupRouteStarted), Is.True);
            Assert.That(job.State, Is.EqualTo(FreightJobState.TravelingToPickup));

            var staleRoute = new FreightExecutionCorrelation(
                allocation.Id, 0, execution.ExecutionId, "personnel-route-old", "stable-route-A");
            Assert.That(manager.ReportExecution(job, execution, staleRoute,
                FreightExecutionReport.Failed, FreightFailureReason.FreightRouteFailed), Is.False);
            Assert.That(job.State, Is.EqualTo(FreightJobState.TravelingToPickup));
            Assert.That(token.IsActive, Is.True);

            var staleExecution = new FreightExecutionCorrelation(
                allocation.Id, 0, "freight-execution-old", "personnel-route-1", "stable-route-A");
            Assert.That(manager.ReportExecution(job, execution, staleExecution,
                FreightExecutionReport.Failed, FreightFailureReason.FreightRouteFailed), Is.False);
            Assert.That(job.State, Is.EqualTo(FreightJobState.TravelingToPickup));

            Assert.That(manager.ReportExecution(job, execution, started,
                FreightExecutionReport.Failed, FreightFailureReason.FreightRouteFailed), Is.True);
            Assert.That(job.State, Is.EqualTo(FreightJobState.Cancelled));
            Assert.That(token.IsActive, Is.False);
        }

        private LogisticsStockComponent CreateStockWithInventory(
            string objectName,
            out InventoryComponent inventory)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            inventory = gameObject.AddComponent<InventoryComponent>();
            return gameObject.AddComponent<LogisticsStockComponent>();
        }

        private sealed class TestExecution : IFreightLegExecution
        {
            public TestExecution(string executionId) => ExecutionId = executionId;
            public string ExecutionId { get; }
            public int LegIndex => 0;
            public bool IsActive { get; private set; } = true;
            public bool IsEmergency => false;
            public float PositioningDistance => 0f;
            public InventoryComponent CargoInventory => null;
            public Object ProviderContext => null;

            public void Complete(FreightDeliveryJob job) => IsActive = false;
        }
    }
}
