using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class ModuleConnectionPointTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                if (roots[i] != null)
                    Object.DestroyImmediate(roots[i]);
            }

            roots.Clear();
        }

        [Test]
        public void PointsAtThresholdConnectAndRepeatedDiscoveryIsIdempotent()
        {
            ModuleConnectionPoint a = CreatePoint("A", Vector3.zero);
            ModuleConnectionPoint b = CreatePoint("B", new Vector3(0.25f, 0f, 0f));
            a.MaxPartnerDistance = 0.25f;
            b.MaxPartnerDistance = 0.25f;

            int firstPass = ModuleConnectionPoint.DiscoverAndConnect();
            Unity.AI.Navigation.NavMeshLink link = a.CurrentLink;
            int secondPass = ModuleConnectionPoint.DiscoverAndConnect();

            Assert.That(firstPass, Is.EqualTo(1));
            Assert.That(secondPass, Is.EqualTo(0));
            Assert.That(a.CurrentPartner, Is.SameAs(b));
            Assert.That(b.CurrentPartner, Is.SameAs(a));
            Assert.That(a.CurrentLink, Is.SameAs(link));
            Assert.That(link.startTransform, Is.SameAs(a.WalkAnchor));
            Assert.That(link.endTransform, Is.SameAs(b.WalkAnchor));
            Assert.That(link.bidirectional, Is.True);
        }

        [Test]
        public void PointsOnTheSameSurfaceAreExcluded()
        {
            GameObject root = CreateRoot("Shared");
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            ModuleConnectionPoint a = CreatePointUnderRoot(root, surface, "A", Vector3.zero);
            ModuleConnectionPoint b = CreatePointUnderRoot(root, surface, "B", new Vector3(0.1f, 0f, 0f));

            Assert.That(ModuleConnectionPoint.DiscoverAndConnect(), Is.EqualTo(0));
            Assert.That(a.IsConnected, Is.False);
            Assert.That(b.IsConnected, Is.False);
        }

        [Test]
        public void NearestAvailablePairingClaimsEachEndpointOnce()
        {
            ModuleConnectionPoint a = CreatePoint("A", Vector3.zero);
            ModuleConnectionPoint b = CreatePoint("B", new Vector3(0.10f, 0f, 0f));
            ModuleConnectionPoint c = CreatePoint("C", new Vector3(0.20f, 0f, 0f));
            a.MaxPartnerDistance = 0.25f;
            b.MaxPartnerDistance = 0.25f;
            c.MaxPartnerDistance = 0.25f;

            Assert.That(ModuleConnectionPoint.DiscoverAndConnect(), Is.EqualTo(1));
            Assert.That(a.IsConnected, Is.True);
            Assert.That(b.IsConnected, Is.True);
            Assert.That(c.IsConnected, Is.False);
        }

        [Test]
        public void EqualDistanceCandidatesUseTheSameDeterministicPairOnRediscovery()
        {
            ModuleConnectionPoint center = CreatePoint("Center", Vector3.zero);
            ModuleConnectionPoint left = CreatePoint("Left", new Vector3(-0.1f, 0f, 0f));
            ModuleConnectionPoint right = CreatePoint("Right", new Vector3(0.1f, 0f, 0f));
            center.MaxPartnerDistance = 0.25f;
            left.MaxPartnerDistance = 0.25f;
            right.MaxPartnerDistance = 0.25f;

            Assert.That(ModuleConnectionPoint.DiscoverAndConnect(), Is.EqualTo(1));
            ModuleConnectionPoint firstPartner = center.CurrentPartner;
            Assert.That(firstPartner, Is.Not.Null);

            center.Disconnect();

            Assert.That(ModuleConnectionPoint.DiscoverAndConnect(), Is.EqualTo(1));
            Assert.That(center.CurrentPartner, Is.SameAs(firstPartner));
        }

        [Test]
        public void DisconnectClearsBothEndpoints()
        {
            ModuleConnectionPoint a = CreatePoint("A", Vector3.zero);
            ModuleConnectionPoint b = CreatePoint("B", new Vector3(0.1f, 0f, 0f));
            ModuleConnectionPoint.DiscoverAndConnect();
            Unity.AI.Navigation.NavMeshLink link = a.CurrentLink;

            a.Disconnect();

            Assert.That(a.CurrentPartner, Is.Null);
            Assert.That(b.CurrentPartner, Is.Null);
            Assert.That(a.CurrentLink, Is.Null);
            Assert.That(b.CurrentLink, Is.Null);
            Assert.That(link == null, Is.True);
        }

        private ModuleConnectionPoint CreatePoint(string name, Vector3 position)
        {
            GameObject root = CreateRoot(name + "Module");
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            return CreatePointUnderRoot(root, surface, name, position);
        }

        private ModuleConnectionPoint CreatePointUnderRoot(
            GameObject root,
            NavMeshSurface surface,
            string name,
            Vector3 position)
        {
            GameObject node = new GameObject(name);
            roots.Add(node);
            node.transform.SetParent(root.transform, false);
            node.transform.position = position;

            GameObject anchorObject = new GameObject("WalkAnchor");
            roots.Add(anchorObject);
            anchorObject.transform.SetParent(node.transform, false);
            anchorObject.transform.localPosition = Vector3.back * 0.5f;

            ModuleConnectionPoint point = node.AddComponent<ModuleConnectionPoint>();
            point.OwnerSurface = surface;
            point.WalkAnchor = anchorObject.transform;
            point.MaxPartnerDistance = 0.25f;
            point.LinkWidth = 0.5f;
            return point;
        }

        private GameObject CreateRoot(string name)
        {
            GameObject root = new GameObject(name);
            roots.Add(root);
            return root;
        }
    }
}
