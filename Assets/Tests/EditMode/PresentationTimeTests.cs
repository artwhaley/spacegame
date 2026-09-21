using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class PresentationTimeTests
    {
        private GameObject managerObject;
        private SimulationManager manager;

        [TearDown]
        public void TearDown()
        {
            if (managerObject != null)
                Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SimulationManagerUsesSpeedMultiplierAsPresentationFactor()
        {
            manager = CreateManager();

            manager.speedMultiplier = 1f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(1f));

            manager.speedMultiplier = 5f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(5f));
        }

        [Test]
        public void PausedSimulationStopsPresentation()
        {
            manager = CreateManager();
            manager.speedMultiplier = 5f;
            manager.paused = true;

            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(0f));
        }

        [Test]
        public void PresentationTimeReadsRegisteredSourceAndUnregistersIt()
        {
            TestPresentationSource source = new TestPresentationSource(3f);
            PresentationTime.RegisterSource(source);

            Assert.That(PresentationTime.SpeedFactor, Is.EqualTo(3f));
            Assert.That(PresentationTime.DeltaTime, Is.GreaterThanOrEqualTo(0f));

            PresentationTime.UnregisterSource(source);

            Assert.That(PresentationTime.SpeedFactor, Is.EqualTo(1f));
        }

        private SimulationManager CreateManager()
        {
            managerObject = new GameObject("Presentation Time Test Manager");
            return managerObject.AddComponent<SimulationManager>();
        }

        private sealed class TestPresentationSource : IPresentationSpeedSource
        {
            public TestPresentationSource(float factor)
            {
                PresentationSpeedFactor = factor;
            }

            public float PresentationSpeedFactor { get; }
        }
    }
}
