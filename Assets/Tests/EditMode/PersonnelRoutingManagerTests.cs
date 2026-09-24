using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class PersonnelRoutingManagerTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private ColonistIdentity person;
        private Transform destination;
        private PersonnelRoutingManager manager;

        [SetUp]
        public void SetUp()
        {
            person = CreateObject("Bob").AddComponent<ColonistIdentity>();
            person.transform.position = new Vector3(1f, 0f, 2f);
            destination = CreateObject("Farm Approach").transform;
            destination.position = new Vector3(5f, 0f, 6f);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
            {
                if (sceneObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(sceneObjects[index]);
            }
        }

        [Test]
        public void ReachableLocalDestinationProducesOneWalkLeg()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(true, 7.5f));

            bool routed = manager.TryPlanRoute(
                person,
                destination,
                out PersonnelRoutePlan plan,
                out string reason);

            Assert.That(routed, Is.True, reason);
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Person, Is.SameAs(person));
            Assert.That(plan.FinalDestination, Is.SameAs(destination));
            Assert.That(plan.Legs, Has.Count.EqualTo(1));
            Assert.That(plan.Legs[0].Type, Is.EqualTo(PersonnelRouteLegType.Walk));
            Assert.That(plan.Legs[0].Destination, Is.SameAs(destination));
        }

        [Test]
        public void UnreachableDestinationProducesNoRoute()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(false, 0f));

            bool routed = manager.TryPlanRoute(
                person,
                destination,
                out PersonnelRoutePlan plan,
                out string reason);

            Assert.That(routed, Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(reason, Is.EqualTo(PersonnelRoutingManager.NoRouteReason));
        }

        [Test]
        public void RouteEstimateExposesUsefulDistance()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(true, 12.25f));

            bool estimated = manager.TryEstimate(
                person,
                destination,
                out PersonnelRouteEstimate estimate);

            Assert.That(estimated, Is.True);
            Assert.That(estimate.StartPosition, Is.EqualTo(person.transform.position));
            Assert.That(estimate.DestinationPosition, Is.EqualTo(destination.position));
            Assert.That(estimate.Distance, Is.EqualTo(12.25f));
        }

        [Test]
        public void HypotheticalStartEstimateIsForwardedToProvider()
        {
            StubPersonnelRouteProvider provider =
                new StubPersonnelRouteProvider(true, 4f);
            manager = CreateManager(provider);
            Vector3 hypotheticalStart = new Vector3(20f, 0f, -3f);

            bool estimated = manager.TryEstimateFrom(
                person,
                hypotheticalStart,
                destination,
                out PersonnelRouteEstimate estimate);

            Assert.That(estimated, Is.True);
            Assert.That(provider.HypotheticalStartQueries, Is.EqualTo(1));
            Assert.That(provider.LastHypotheticalStart, Is.EqualTo(hypotheticalStart));
            Assert.That(estimate.StartPosition, Is.EqualTo(hypotheticalStart));
        }

        [Test]
        public void RoutePlanCanBeBuiltWithoutBrainOrActivityPolicy()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(true, 3f));
            Assert.That(person.GetComponent<ColonistBrain>(), Is.Null);
            Assert.That(
                person.GetComponent<Colony.Interactions.ColonistActivityRunner>(),
                Is.Null);

            bool routed = manager.TryPlanRoute(
                person,
                destination,
                out PersonnelRoutePlan plan,
                out string reason);

            Assert.That(routed, Is.True, reason);
            Assert.That(plan.Legs, Has.Count.EqualTo(1));
            Assert.That(plan.TotalEstimatedDistance, Is.EqualTo(3f));
        }

        [Test]
        public void RepeatedIdenticalQueriesReturnEquivalentDeterministicPlans()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(true, 8f));

            manager.TryPlanRoute(person, destination, out PersonnelRoutePlan first, out _);
            manager.TryPlanRoute(person, destination, out PersonnelRoutePlan second, out _);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second.StableId, Is.EqualTo(first.StableId));
            Assert.That(second.TotalEstimatedDistance, Is.EqualTo(first.TotalEstimatedDistance));
            Assert.That(second.FinalDestination, Is.SameAs(first.FinalDestination));
            Assert.That(second.Legs[0].Type, Is.EqualTo(first.Legs[0].Type));
        }

        [Test]
        public void B1PlansCannotContainShuttleLegs()
        {
            manager = CreateManager(new StubPersonnelRouteProvider(true, 5f));
            manager.TryPlanRoute(person, destination, out PersonnelRoutePlan plan, out _);
            string[] legKinds = Enum.GetNames(typeof(PersonnelRouteLegType));

            Assert.That(legKinds, Is.EqualTo(new[] { "Walk" }));
            for (int index = 0; index < plan.Legs.Count; index++)
                Assert.That(plan.Legs[index].Type, Is.EqualTo(PersonnelRouteLegType.Walk));
        }

        private PersonnelRoutingManager CreateManager(
            IPersonnelRouteProvider provider)
        {
            PersonnelRoutingManager created =
                CreateObject("PersonnelRoutingManager").AddComponent<PersonnelRoutingManager>();
            created.ConfigureProvider(provider);
            return created;
        }

        private GameObject CreateObject(string name)
        {
            GameObject created = new GameObject(name);
            sceneObjects.Add(created);
            return created;
        }

        private sealed class StubPersonnelRouteProvider : IPersonnelRouteProvider
        {
            private readonly bool reachable;
            private readonly float distance;

            public StubPersonnelRouteProvider(bool reachable, float distance)
            {
                this.reachable = reachable;
                this.distance = distance;
            }

            public int HypotheticalStartQueries { get; private set; }
            public Vector3 LastHypotheticalStart { get; private set; }

            public bool TryEstimate(
                ColonistIdentity person,
                Transform destination,
                out PersonnelRouteEstimate estimate)
            {
                return TryEstimateFrom(
                    person,
                    person != null ? person.transform.position : Vector3.zero,
                    destination,
                    out estimate);
            }

            public bool TryEstimateFrom(
                ColonistIdentity person,
                Vector3 hypotheticalStart,
                Transform destination,
                out PersonnelRouteEstimate estimate)
            {
                estimate = default;
                HypotheticalStartQueries++;
                LastHypotheticalStart = hypotheticalStart;
                if (!reachable || person == null || destination == null)
                    return false;

                estimate = new PersonnelRouteEstimate(
                    hypotheticalStart,
                    destination.position,
                    distance);
                return true;
            }
        }
    }
}
