using AsteroidColony;
using NUnit.Framework;

namespace AsteroidColony.Tests
{
    /// <summary>
    /// B1.5 keeps worker execution out of freight data and colonist policy.
    /// These public architecture boundaries are important B2 prerequisites.
    /// </summary>
    public sealed class B1_5ArchitectureBoundaryTests
    {
        [Test]
        public void FreightAllocationDoesNotOwnAWorker()
        {
            Assert.That(typeof(FreightAllocation).GetProperty("Carrier"), Is.Null);
        }

        [Test]
        public void FreightRoutePlanDoesNotOwnAWorker()
        {
            Assert.That(typeof(LogisticsRoutePlan).GetProperty("Carrier"), Is.Null);
        }

        [Test]
        public void FreightLogisticsUsesWorkplaceServiceBoundary()
        {
            Assert.That(typeof(FreightLogisticsManager).GetProperty("RoutineCarrierRole"), Is.Null);
            Assert.That(typeof(WalkingFreightWorkService).GetProperty("Active"), Is.Not.Null);
            Assert.That(typeof(FreightWorkQuote).GetProperty("Provider"), Is.Not.Null);
            Assert.That(typeof(WalkingFreightExecution).GetProperty("Worker"), Is.Null);
        }

        [Test]
        public void ColonistBrainDoesNotExposeFreightExcursionPolicy()
        {
            var brainType = typeof(ColonistBrain);
            Assert.That(brainType.GetProperty("WorkExcursionReady"), Is.Null);
            Assert.That(brainType.GetProperty("ShouldAbortWorkExcursionBeforePickup"), Is.Null);
            Assert.That(brainType.GetMethod("CanBeginWorkExcursion"), Is.Null);
            Assert.That(brainType.GetMethod("TryBeginWorkExcursion"), Is.Null);
            Assert.That(brainType.GetMethod("SetWorkExcursionCargo"), Is.Null);
            Assert.That(brainType.GetMethod("CompleteWorkExcursion"), Is.Null);
        }

        [Test]
        public void FreightExecutionComponentsAreNotColonistRuntimeTypes()
        {
            var runtimeAssembly = typeof(ColonistBrain).Assembly;
            Assert.That(runtimeAssembly.GetType(
                "AsteroidColony.WalkingFreightCarrierComponent"), Is.Null);
            Assert.That(runtimeAssembly.GetType(
                "AsteroidColony.WalkingFreightRunner"), Is.Null);
        }
    }
}
