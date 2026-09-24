using System.Collections.Generic;
using AsteroidColony;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class B1_6FreightLegShapeTests
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
        public void FreightRouteCanRepresentWalkingShuttleWalkingWithoutShuttleExecution()
        {
            LogisticsStockComponent source = CreateStock("Source");
            LogisticsStockComponent handoff = CreateStock("Handoff");
            LogisticsStockComponent baseStock = CreateStock("Base");
            LogisticsStockComponent destination = CreateStock("Destination");
            resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.name = "Synthetic freight resource";

            var order = new FreightOrder("freight-order-test", destination, resource, 1f, 0L);
            var route = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, handoff, 1f),
                new LogisticsRouteLeg(LogisticsRouteLegType.ShuttleFreight, handoff, baseStock, 2f),
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, baseStock, destination, 1f)
            });
            var allocation = new FreightAllocation(
                "freight-allocation-test", order, source, 1f, route);
            InventoryComponent inventory = source.gameObject.AddComponent<InventoryComponent>();
            Assert.That(inventory.SetCapacity(resource, 1f), Is.True);
            Assert.That(inventory.Add(resource, 1f), Is.EqualTo(1f));
            Assert.That(inventory.TryReserveOwned(resource, 1f, out InventoryReservationToken token), Is.True);

            object managerAuthority = new object();
            var firstExecution = new TestLegExecution("walking-execution", 0);
            var job = new FreightDeliveryJob(allocation, firstExecution, token, managerAuthority);
            Assert.That(job.RoutePlan.Legs.Count, Is.EqualTo(3));
            Assert.That(job.CurrentLeg.Type, Is.EqualTo(LogisticsRouteLegType.WalkingCarrier));

            Assert.That(job.AdvanceLeg(managerAuthority), Is.True);
            job.SetActiveLegExecution(managerAuthority, null);
            Assert.That(job.CurrentLegIndex, Is.EqualTo(1));
            Assert.That(job.CurrentLeg.Type, Is.EqualTo(LogisticsRouteLegType.ShuttleFreight));
            Assert.That(job.IsAwaitingLegAssignment, Is.True);

            Assert.That(job.AdvanceLeg(managerAuthority), Is.True);
            Assert.That(job.CurrentLegIndex, Is.EqualTo(2));
            Assert.That(job.CurrentLeg.Type, Is.EqualTo(LogisticsRouteLegType.WalkingCarrier));
            Assert.That(typeof(FreightDeliveryJob).GetProperty("ActiveLegExecution").PropertyType,
                Is.EqualTo(typeof(IFreightLegExecution)));
        }

        private LogisticsStockComponent CreateStock(string objectName)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            return gameObject.AddComponent<LogisticsStockComponent>();
        }

        private sealed class TestLegExecution : IFreightLegExecution
        {
            public TestLegExecution(string executionId, int legIndex)
            {
                ExecutionId = executionId;
                LegIndex = legIndex;
            }

            public string ExecutionId { get; }
            public int LegIndex { get; }
            public bool IsActive => true;
            public bool IsEmergency => false;
            public float PositioningDistance => 0f;
            public InventoryComponent CargoInventory => null;
            public Object ProviderContext => null;
            public void Complete(FreightDeliveryJob job) { }
        }
    }
}
