using System.Globalization;
using System.IO;
using System.Reflection;
using AsteroidColony;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class B1_6IdentityDeterminismTests
    {
        [Test]
        public void SharedSceneKeyPreservesSceneAndHierarchyFormat()
        {
            GameObject root = new GameObject("IdentityRoot");
            GameObject child = new GameObject("IdentityChild");
            child.transform.SetParent(root.transform, false);
            try
            {
                string scene = string.IsNullOrEmpty(child.scene.path)
                    ? child.scene.name
                    : child.scene.path;
                string expected = scene + "/" +
                    root.name + "[" + root.transform.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]/" +
                    child.name + "[" + child.transform.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]/";

                Assert.That(SceneStableIdentity.GetKey(child.transform), Is.EqualTo(expected));
                Assert.That(PersonnelRouteIdentity.GetStableKey(child.transform), Is.EqualTo(expected));
                Assert.That(child.AddComponent<LogisticsStockComponent>().GetStableKey(), Is.EqualTo(expected));
                Assert.That(child.AddComponent<WalkingFreightWorkService>().GetStableKey(), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LogisticsTickPrioritiesKeepPublicationDispatchExecutionOrder()
        {
            Assert.That(SimulationTickPriorities.LogisticsStockPublication, Is.EqualTo(300));
            Assert.That(SimulationTickPriorities.LogisticsDispatch, Is.EqualTo(310));
            Assert.That(SimulationTickPriorities.LogisticsWorkExecution, Is.EqualTo(320));
            Assert.That(SimulationTickPriorities.LogisticsStockPublication,
                Is.LessThan(SimulationTickPriorities.LogisticsDispatch));
            Assert.That(SimulationTickPriorities.LogisticsDispatch,
                Is.LessThan(SimulationTickPriorities.LogisticsWorkExecution));
        }

        [Test]
        public void FreightExecutionCorrelationCarriesLegAndRouteIdentity()
        {
            var correlation = new FreightExecutionCorrelation(
                "freight-allocation-000001", 2, "freight-execution-000003",
                "personnel-route-7", "scene/FreightWorker[0]/Destination[1]/");

            Assert.That(correlation.AllocationId, Is.EqualTo("freight-allocation-000001"));
            Assert.That(correlation.LegIndex, Is.EqualTo(2));
            Assert.That(correlation.ExecutionId, Is.EqualTo("freight-execution-000003"));
            Assert.That(correlation.HasPersonnelRoute, Is.True);
            Assert.That(correlation.HasPartialPersonnelRoute, Is.False);
            Assert.That(new FreightExecutionCorrelation("a", 0, "e", "route", null)
                .HasPartialPersonnelRoute, Is.True);
        }

        [Test]
        public void FreightJobUsesAllocationAndPerExecutionIdentities()
        {
            Assert.That(typeof(FreightDeliveryJob).GetProperty("Id"), Is.Null);
            Assert.That(typeof(FreightDeliveryJob).GetProperty("ActivePersonnelRouteId"), Is.Not.Null);
            Assert.That(typeof(IFreightLegExecution).GetProperty("ExecutionId").PropertyType,
                Is.EqualTo(typeof(string)));

            MethodInfo report = typeof(FreightLogisticsManager).GetMethod(
                "ReportExecution", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(report, Is.Not.Null);
            Assert.That(report.GetParameters()[2].ParameterType,
                Is.EqualTo(typeof(FreightExecutionCorrelation)));
        }

        [Test]
        public void WalkingFreightCorrelatesWholePersonnelRoutesByRouteToken()
        {
            string path = Path.Combine(Application.dataPath,
                "Scripts/ColonyPrototype/Logistics/P4b/WalkingFreightExecution.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("ApproachRouteCompleted += HandleApproachRouteCompleted"));
            Assert.That(source, Does.Contain("ApproachRouteFailed += HandleApproachRouteFailed"));
            Assert.That(source, Does.Contain("string.Equals(activeRouteId, routeId, StringComparison.Ordinal)"));
            Assert.That(source, Does.Not.Contain("plan.FinalDestination =="));
        }

        [Test]
        public void GenericFreightIdsUseInvariantCountersWithoutASecondJobName()
        {
            string path = Path.Combine(Application.dataPath,
                "Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("\"freight-order-\""));
            Assert.That(source, Does.Contain("\"freight-allocation-\""));
            Assert.That(source, Does.Contain("\"freight-execution-\""));
            Assert.That(source, Does.Contain("ToString(\"D6\", CultureInfo.InvariantCulture)"));
            Assert.That(source, Does.Not.Contain("food-order-"));
            Assert.That(source, Does.Not.Contain("freight-job-"));
            Assert.That(source, Does.Not.Contain("SimulationLogField(\"jobId\""));
        }

    }
}
