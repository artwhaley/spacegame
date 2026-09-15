using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ContractOrderingTests
    {
        private GameObject managerObject;
        private GameObject sourceObject;
        private GameObject destinationObject;
        private GameObject colonistObject;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Contract Manager Test");
            managerObject.AddComponent<ContractManager>();
            sourceObject = new GameObject("Source");
            destinationObject = new GameObject("Destination");
            colonistObject = new GameObject("Passenger");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(colonistObject);
            Object.DestroyImmediate(destinationObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void HigherPriorityOpenContractWins()
        {
            LocationAnchor source = sourceObject.AddComponent<LocationAnchor>();
            LocationAnchor destination = destinationObject.AddComponent<LocationAnchor>();
            ColonistAgent passenger = colonistObject.AddComponent<ColonistAgent>();
            passenger.currentLocation = source;

            TransportContract low = ContractManager.Instance.CreatePassengerContract(
                source, destination, new List<ColonistAgent> { passenger }, 2);
            TransportContract high = ContractManager.Instance.CreatePassengerContract(
                source, destination, new List<ColonistAgent> { passenger }, 8);

            Assert.That(ContractManager.Instance.FindBestOpenContract(), Is.EqualTo(high));
            Assert.That(low.state, Is.EqualTo(TransportContractState.Open));
        }

        [Test]
        public void EqualPriorityUsesOldestContract()
        {
            LocationAnchor source = sourceObject.AddComponent<LocationAnchor>();
            LocationAnchor destination = destinationObject.AddComponent<LocationAnchor>();
            ColonistAgent passenger = colonistObject.AddComponent<ColonistAgent>();
            passenger.currentLocation = source;

            TransportContract oldest = ContractManager.Instance.CreatePassengerContract(
                source, destination, new List<ColonistAgent> { passenger }, 5);
            ContractManager.Instance.CreatePassengerContract(
                source, destination, new List<ColonistAgent> { passenger }, 5);

            Assert.That(ContractManager.Instance.FindBestOpenContract(), Is.EqualTo(oldest));
        }
    }
}
