using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ResourceDepositTests
    {
        private GameObject depositObject;
        private ResourceDeposit deposit;

        [SetUp]
        public void SetUp()
        {
            depositObject = new GameObject("Deposit Test");
            deposit = depositObject.AddComponent<ResourceDeposit>();
            deposit.displayName = "Test Deposit";
            deposit.startingQuantity = 5f;
            FieldInfo remaining = typeof(ResourceDeposit).GetField(
                "remainingQuantity", BindingFlags.Instance | BindingFlags.NonPublic);
            remaining.SetValue(deposit, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(depositObject);
        }

        [Test]
        public void ExtractionIsFinite()
        {
            Assert.That(deposit.Extract(3f), Is.EqualTo(3f));
            Assert.That(deposit.Extract(3f), Is.EqualTo(2f));
            Assert.That(deposit.RemainingQuantity, Is.EqualTo(0f));
            Assert.That(deposit.Extract(1f), Is.EqualTo(0f));
        }

        [Test]
        public void PartialFinalExtractionDoesNotGoNegative()
        {
            Assert.That(deposit.Extract(4.25f), Is.EqualTo(4.25f));
            Assert.That(deposit.Extract(4.25f), Is.EqualTo(0.75f));
            Assert.That(deposit.RemainingQuantity, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
