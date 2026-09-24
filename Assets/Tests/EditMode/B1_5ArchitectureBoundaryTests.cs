using AsteroidColony;
using NUnit.Framework;
using System.Reflection;

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
            Assert.That(typeof(WalkingFreightWorkService).GetInterfaces(),
                Does.Contain(typeof(IWorkExecutionOwner)));
        }

        [Test]
        public void FreightLogisticsManagerApiDoesNotBindAColonistWorker()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;
            var managerType = typeof(FreightLogisticsManager);
            foreach (FieldInfo field in managerType.GetFields(flags))
                Assert.That(ContainsColonistBinding(field.FieldType), Is.False, field.Name);

            foreach (MethodInfo method in managerType.GetMethods(flags))
            {
                Assert.That(ContainsColonistBinding(method.ReturnType), Is.False, method.Name);
                foreach (ParameterInfo parameter in method.GetParameters())
                    Assert.That(ContainsColonistBinding(parameter.ParameterType), Is.False,
                        method.Name + "(" + parameter.Name + ")");
            }
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
            Assert.That(brainType.GetField("workExcursionHasCargo",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
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

        private static bool ContainsColonistBinding(Type type)
        {
            if (type == typeof(ColonistIdentity) || type.Name.Contains("WalkingFreightCarrier"))
                return true;
            if (type.IsArray)
                return ContainsColonistBinding(type.GetElementType());
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments())
                    if (ContainsColonistBinding(argument))
                        return true;
            return false;
        }
    }
}
