using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.TestTools;

namespace AsteroidColony.Tests
{
    public sealed class ModuleConnectionPointPlayModeTests
    {
        private GameObject firstRoot;
        private GameObject secondRoot;

        [UnityTest]
        public IEnumerator StartupPassConnectsRegisteredPoints()
        {
            firstRoot = CreateModule("PlayA", Vector3.zero);
            secondRoot = CreateModule("PlayB", new Vector3(0.1f, 0f, 0f));

            yield return null;

            ModuleConnectionPoint first = firstRoot.GetComponentInChildren<ModuleConnectionPoint>();
            ModuleConnectionPoint second = secondRoot.GetComponentInChildren<ModuleConnectionPoint>();
            Assert.That(first.CurrentPartner, Is.SameAs(second));
            Assert.That(second.CurrentPartner, Is.SameAs(first));
            Assert.That(first.CurrentLink, Is.Not.Null);
            Assert.That(first.CurrentLink.bidirectional, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingAnEndpointClearsTheRuntimeLink()
        {
            firstRoot = CreateModule("PlayA", Vector3.zero);
            secondRoot = CreateModule("PlayB", new Vector3(0.1f, 0f, 0f));
            yield return null;

            ModuleConnectionPoint first = firstRoot.GetComponentInChildren<ModuleConnectionPoint>();
            ModuleConnectionPoint second = secondRoot.GetComponentInChildren<ModuleConnectionPoint>();
            first.enabled = false;
            yield return null;

            Assert.That(first.CurrentPartner, Is.Null);
            Assert.That(second.CurrentPartner, Is.Null);
            Assert.That(second.CurrentLink, Is.Null);
        }

        [UnityTest]
        public IEnumerator ExplicitDiscoveryConnectsAModuleAddedAfterStartup()
        {
            firstRoot = CreateModule("PlayA", Vector3.zero);
            yield return null;

            secondRoot = CreateModule("PlayB", new Vector3(0.1f, 0f, 0f));
            yield return null;

            ModuleConnectionPoint first = firstRoot.GetComponentInChildren<ModuleConnectionPoint>();
            ModuleConnectionPoint second = secondRoot.GetComponentInChildren<ModuleConnectionPoint>();
            Assert.That(first.CurrentPartner, Is.Null);
            Assert.That(ModuleConnectionPoint.DiscoverAndConnect(), Is.EqualTo(1));
            Assert.That(first.CurrentPartner, Is.SameAs(second));
            Assert.That(second.CurrentPartner, Is.SameAs(first));
        }

        [TearDown]
        public IEnumerator TearDown()
        {
            if (firstRoot != null)
                Object.Destroy(firstRoot);
            if (secondRoot != null)
                Object.Destroy(secondRoot);
            yield return null;
        }

        private static GameObject CreateModule(string name, Vector3 position)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            GameObject node = new GameObject("nodeConnect");
            node.transform.SetParent(root.transform, false);
            GameObject anchor = new GameObject("WalkAnchor");
            anchor.transform.SetParent(node.transform, false);
            anchor.transform.localPosition = Vector3.back * 0.5f;

            ModuleConnectionPoint point = node.AddComponent<ModuleConnectionPoint>();
            point.OwnerSurface = surface;
            point.WalkAnchor = anchor.transform;
            return root;
        }
    }
}
