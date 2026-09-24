using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class B1_6LegacyTransportBoundaryTests
    {
        [Test]
        public void ModernP4bSourcesAvoidLegacyTransportAuthorities()
        {
            string[] sources =
            {
                "Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs",
                "Scripts/ColonyPrototype/Logistics/P4b/WalkingFreightWorkService.cs",
                "Scripts/ColonyPrototype/Logistics/P4b/WalkingFreightExecution.cs",
                "Scripts/ColonyPrototype/Logistics/P4b/LogisticsRoutePlan.cs"
            };
            string[] legacyNames = { "LogisticsManager", "ContractManager", "TransportContract" };
            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                string path = Path.Combine(Application.dataPath, sources[sourceIndex]);
                string source = File.ReadAllText(path);
                for (int legacyIndex = 0; legacyIndex < legacyNames.Length; legacyIndex++)
                    Assert.That(Regex.IsMatch(source,
                            @"(?<![A-Za-z0-9_])" + legacyNames[legacyIndex] + @"(?![A-Za-z0-9_])"),
                        Is.False,
                        sources[sourceIndex] + " references " + legacyNames[legacyIndex]);
            }
        }

        [Test]
        public void B1AndP4bFixtureValidatorsShareTheLegacyTransportGuard()
        {
            string editorRoot = Path.Combine(Application.dataPath, "Editor/Logistics");
            string b1 = File.ReadAllText(Path.Combine(editorRoot, "B1ModularSceneAuthoring.cs"));
            string p4b = File.ReadAllText(Path.Combine(editorRoot, "P4bModularSceneAuthoring.cs"));
            string guard = File.ReadAllText(Path.Combine(editorRoot, "LegacyTransportSceneGuard.cs"));

            Assert.That(b1, Does.Contain("LegacyTransportSceneGuard.Validate"));
            Assert.That(p4b, Does.Contain("LegacyTransportSceneGuard.Validate"));
            Assert.That(guard, Does.Contain("Check<LogisticsManager>"));
            Assert.That(guard, Does.Contain("Check<ContractManager>"));
            Assert.That(guard, Does.Contain("Check<TransportExecutorComponent>"));
        }

        [Test]
        public void LegacyTransportSourcesAreUnderExplicitBoundaryAndKeepTheirMetaFiles()
        {
            string legacyRoot = Path.Combine(Application.dataPath,
                "Scripts/ColonyPrototype/LegacyTransport");
            string[] scripts =
            {
                "LogisticsManager.cs", "ContractManager.cs", "TransportContract.cs",
                "TransportDispatchCandidate.cs", "FreightDemand.cs", "TransportPriorityRules.cs",
                "ResourceStockPolicyComponent.cs", "TransportVehicleComponent.cs",
                "TransportExecutorComponent.cs", "PassengerCarrierComponent.cs"
            };

            foreach (string script in scripts)
            {
                Assert.That(File.Exists(Path.Combine(legacyRoot, script)), Is.True, script);
                Assert.That(File.Exists(Path.Combine(legacyRoot, script + ".meta")), Is.True,
                    script + " GUID sidecar");
            }
        }
    }
}
