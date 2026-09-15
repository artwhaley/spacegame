using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class StockPolicyTests
    {
        private GameObject contractObject;
        private GameObject logisticsObject;
        private GameObject policyObject;
        private InventoryComponent policyInventory;
        private LocationAnchor policyLocation;
        private ResourceDefinition water;
        private ResourceDefinition discrete;
        private ResourceStockPolicyComponent policy;

        [SetUp]
        public void SetUp()
        {
            water = CreateResource("Water", ResourceQuantityMode.Fractional);
            discrete = CreateResource("Discrete", ResourceQuantityMode.Discrete);
            contractObject = new GameObject("Contract Manager");
            contractObject.AddComponent<ContractManager>();
            logisticsObject = new GameObject("Logistics Manager");
            logisticsObject.AddComponent<LogisticsManager>();
            policyObject = new GameObject("Policy");
            policyLocation = policyObject.AddComponent<LocationAnchor>();
            policyInventory = policyObject.AddComponent<InventoryComponent>();
            policy = policyObject.AddComponent<ResourceStockPolicyComponent>();
            Configure(policyInventory, water, 20f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(policyObject);
            Object.DestroyImmediate(logisticsObject);
            Object.DestroyImmediate(contractObject);
            Object.DestroyImmediate(water);
            Object.DestroyImmediate(discrete);
        }

        [Test]
        public void ForegroundDemandActivatesAtThreshold()
        {
            ResourceStockPolicyEntry entry = ForegroundEntry();
            policy.entries.Add(entry);
            policy.RefreshPolicy();
            FreightDemand demand = LogisticsManager.Instance.Demands[0];
            Assert.That(demand.active, Is.True);
            Assert.That(demand.DesiredQuantity, Is.EqualTo(8f));

            policyInventory.Add(water, 4f);
            policy.RefreshPolicy();
            Assert.That(demand.active, Is.False);
            policyInventory.Remove(water, 2f);
            policy.RefreshPolicy();
            Assert.That(demand.active, Is.True);
            Assert.That(demand.DesiredQuantity, Is.EqualTo(6f));
        }

        [Test]
        public void InboundReducesDemandWithoutIncreasingOnHand()
        {
            ResourceStockPolicyEntry entry = ForegroundEntry();
            policy.entries.Add(entry);
            policy.RefreshPolicy();
            FreightDemand demand = LogisticsManager.Instance.Demands[0];
            GameObject sourceObject = new GameObject("Source");
            InventoryComponent sourceInventory = sourceObject.AddComponent<InventoryComponent>();
            Configure(sourceInventory, water, 20f, 10f);
            LocationAnchor source = sourceObject.AddComponent<LocationAnchor>();
            TransportContract contract = ContractManager.Instance.CreateFreightContract(
                water, 4f, sourceInventory, policyInventory, source, policyLocation, 5);
            contract.demandId = demand.demandId;
            policy.RefreshPolicy();

            Assert.That(policyInventory.GetOnHand(water), Is.EqualTo(0f));
            Assert.That(demand.InboundQuantity, Is.EqualTo(4f));
            Assert.That(demand.DesiredQuantity, Is.EqualTo(4f));
            Object.DestroyImmediate(sourceObject);
        }

        [Test]
        public void BackgroundDemandIsDistinctAndDoesNotOverlapForeground()
        {
            ResourceStockPolicyEntry entry = ForegroundEntry();
            entry.backgroundFillEnabled = true;
            entry.backgroundTargetStock = 16f;
            policy.entries.Add(entry);
            policy.RefreshPolicy();
            Assert.That(LogisticsManager.Instance.Demands.Count, Is.EqualTo(2));
            FreightDemand foreground = LogisticsManager.Instance.Demands[0];
            FreightDemand background = LogisticsManager.Instance.Demands[1];
            Assert.That(foreground.demandClass, Is.EqualTo(FreightDemandClass.Foreground));
            Assert.That(background.demandClass, Is.EqualTo(FreightDemandClass.Background));
            Assert.That(foreground.DesiredQuantity, Is.EqualTo(8f));
            Assert.That(background.DesiredQuantity, Is.EqualTo(0f));
        }

        [Test]
        public void ExportSupplyRespectsRetainStock()
        {
            policyInventory.Add(water, 10f);
            ResourceStockPolicyEntry entry = new ResourceStockPolicyEntry
            {
                resource = water,
                exportEnabled = true,
                retainStock = 3f
            };
            policy.entries.Add(entry);
            policy.RefreshPolicy();
            Assert.That(LogisticsManager.Instance.Supplies[0].retainStock, Is.EqualTo(3f));
            Assert.That(policyInventory.GetAvailable(water), Is.EqualTo(10f));
            policyInventory.Reserve(water, 8f);
            Assert.That(LogisticsManager.Instance.GetExportableQuantity(LogisticsManager.Instance.Supplies[0]), Is.EqualTo(0f));
        }

        [Test]
        public void DiscretePolicyRejectsFractionalStockValues()
        {
            policy.entries.Add(new ResourceStockPolicyEntry
            {
                resource = discrete,
                normalDemandEnabled = true,
                reorderThreshold = 1.5f,
                targetStock = 2f,
                minimumShipment = 1f
            });
            policy.RefreshPolicy();
            Assert.That(LogisticsManager.Instance.Demands.Count, Is.EqualTo(0));
        }

        private ResourceStockPolicyEntry ForegroundEntry()
        {
            return new ResourceStockPolicyEntry
            {
                resource = water,
                normalDemandEnabled = true,
                reorderThreshold = 3f,
                targetStock = 8f,
                priority = 5,
                minimumShipment = 4f,
                maximumShipment = 8f
            };
        }

        private ResourceDefinition CreateResource(string id, ResourceQuantityMode mode)
        {
            ResourceDefinition resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.name = id;
            resource.stableId = id;
            resource.quantityMode = mode;
            return resource;
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
