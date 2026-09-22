using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class ResourceQuantityRulesTests
    {
        private ResourceDefinition fractional;
        private ResourceDefinition discrete;

        [SetUp]
        public void SetUp()
        {
            fractional = ScriptableObject.CreateInstance<ResourceDefinition>();
            fractional.name = "Fractional Test Resource";
            fractional.quantityMode = ResourceQuantityMode.Fractional;

            discrete = ScriptableObject.CreateInstance<ResourceDefinition>();
            discrete.name = "Discrete Test Resource";
            discrete.quantityMode = ResourceQuantityMode.Discrete;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fractional);
            Object.DestroyImmediate(discrete);
        }

        [Test]
        public void FractionalAcceptsPartialAmount()
        {
            Assert.That(ResourceQuantityRules.TryValidate(fractional, 0.2f, out _), Is.True);
        }

        [Test]
        public void DiscreteAcceptsWholeAmount()
        {
            Assert.That(ResourceQuantityRules.TryValidate(discrete, 2f, out _), Is.True);
        }

        [Test]
        public void DiscreteRejectsPartialAmount()
        {
            Assert.That(ResourceQuantityRules.TryValidate(discrete, 0.2f, out _), Is.False);
        }

        [Test]
        public void DiscreteAcceptsAndNormalizesNearInteger()
        {
            Assert.That(ResourceQuantityRules.TryNormalize(discrete, 1.000001f, out float normalized), Is.True);
            Assert.That(normalized, Is.EqualTo(1f));
        }

        [Test]
        public void NegativeAndNonFiniteAmountsAreRejected()
        {
            Assert.That(ResourceQuantityRules.TryValidate(fractional, -0.1f, out _), Is.False);
            Assert.That(ResourceQuantityRules.TryValidate(fractional, float.NaN, out _), Is.False);
            Assert.That(ResourceQuantityRules.TryValidate(fractional, float.PositiveInfinity, out _), Is.False);
        }

        [Test]
        public void ResourceAmountReportsDiscreteFractionalAuthoringError()
        {
            ResourceAmount amount = new ResourceAmount { resource = discrete, amount = 3.5f };
            Assert.That(amount.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("whole quantity"));
        }
    }
}
