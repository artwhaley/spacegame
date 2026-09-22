using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class PresentationTimeTests
    {
        private GameObject managerObject;
        private GameObject animationObject;
        private SimulationManager manager;

        [TearDown]
        public void TearDown()
        {
            if (managerObject != null)
                Object.DestroyImmediate(managerObject);
            if (animationObject != null)
                Object.DestroyImmediate(animationObject);
        }

        [Test]
        public void SimulationManagerUsesSpeedMultiplierAsPresentationFactor()
        {
            manager = CreateManager();

            manager.speedMultiplier = 1f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(1f));

            manager.speedMultiplier = 5f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(5f));

            manager.speedMultiplier = 100f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(100f));

            manager.speedMultiplier = 1000f;
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(1000f));
        }

        [Test]
        public void SimulationManagerUsesRealSecondsForGameClock()
        {
            manager = CreateManager();
            manager.speedMultiplier = 1f;

            AdvanceTick(manager, 1f);

            Assert.That(manager.CurrentGameHour, Is.EqualTo(1f / 3600f).Within(0.0000001f));
        }

        [Test]
        public void LogicalStepMetadataUsesSimulatedSecondsAndCarriesNoLegacyRate()
        {
            manager = CreateManager();
            manager.speedMultiplier = 1000f;

            AdvanceTick(manager, 1f);

            Assert.That(manager.CurrentSimulationSeconds, Is.EqualTo(1000d).Within(0.000001d));
            Assert.That(manager.LogicalStepSimulationSeconds, Is.EqualTo(1f));
            Assert.That(manager.MaxLogicalStepsPerFrame, Is.GreaterThanOrEqualTo(1000));
        }

        [Test]
        public void LogicalDebtIsCappedPerFrameAndCarriedForward()
        {
            manager = CreateManager();
            SetPrivateField(manager, "simulationDebtSeconds", 2.5d);
            SetPrivateField(manager, "maxLogicalStepsPerFrame", 1);

            InvokePrivate(manager, "Update");

            Assert.That(manager.CurrentTick, Is.EqualTo(1));
            Assert.That(manager.SimulationDebtSeconds, Is.GreaterThan(0.5d));
            Assert.That(manager.SimulationDebtSeconds, Is.LessThan(4d));

            InvokePrivate(manager, "Update");
            Assert.That(manager.CurrentTick, Is.EqualTo(2));
        }

        [Test]
        public void OverloadedFrameSlowsLogicalAndPresentationTimeTogether()
        {
            manager = CreateManager();
            manager.speedMultiplier = 1000f;
            SetPrivateField(manager, "maximumPendingSimulationSeconds", 250f);

            InvokePrivate(manager, "AdvanceFrame", 1f);

            Assert.That(manager.LastFrameRequestedSimulationSeconds, Is.EqualTo(1000d));
            Assert.That(manager.LastFrameCommittedSimulationSeconds, Is.EqualTo(250d));
            Assert.That(manager.PresentationSpeedFactor, Is.EqualTo(250f));
            Assert.That(manager.CurrentSimulationSeconds, Is.EqualTo(250d));
            Assert.That(manager.PaceShortfallSeconds, Is.EqualTo(750d));
            Assert.That(manager.AdmissionLimitHitCount, Is.EqualTo(1));
        }

        [Test]
        public void ArmedStopPreventsFrameBatchFromOvershootingEndpoint()
        {
            manager = CreateManager();
            manager.speedMultiplier = 1000f;
            manager.ArmSimulationStop(100d);

            InvokePrivate(manager, "AdvanceFrame", 1f);

            Assert.That(manager.CurrentSimulationSeconds, Is.EqualTo(100d));
            Assert.That(manager.LastFrameCommittedSimulationSeconds, Is.EqualTo(100d));
            Assert.That(manager.paused, Is.True);
            Assert.That(manager.SimulationStopReached, Is.True);
        }

        [Test]
        public void SpeedMultiplierClampsToOneThroughOneThousand()
        {
            manager = CreateManager();

            Assert.That(manager.speedMultiplier, Is.EqualTo(10f));

            manager.SetSpeedMultiplier(0.5f);
            Assert.That(manager.speedMultiplier, Is.EqualTo(1f));

            manager.SetSpeedMultiplier(1500f);
            Assert.That(manager.speedMultiplier, Is.EqualTo(1000f));
        }

        [Test]
        public void HardAnimationFailureResetsAnimatorToLocomotionAtHighSpeed()
        {
            manager = CreateManager();
            manager.speedMultiplier = 1000f;

            animationObject = new GameObject("High Speed Animation Test");
            Animator animator = animationObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Colonists/ColonistHumanoid.controller");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

            ColonistAnimationDriver driver = animationObject.AddComponent<ColonistAnimationDriver>();
            MethodInfo fail = typeof(ColonistAnimationDriver).GetMethod(
                "Fail",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fail, Is.Not.Null);
            fail.Invoke(driver, new object[] { "high-speed watchdog regression" });

            animator.Update(0f);
            Assert.That(
                animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                Is.EqualTo(Animator.StringToHash("Locomotion")));
            Assert.That(animator.speed, Is.EqualTo(1000f));
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

        private static void AdvanceTick(SimulationManager simulationManager, float realDeltaSeconds)
        {
            InvokePrivate(simulationManager, "AdvanceTick", realDeltaSeconds);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
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
