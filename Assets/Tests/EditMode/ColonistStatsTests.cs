using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistStatsTests
    {
        private GameObject statsObject;
        private ColonistStatsComponent stats;
        private GameObject runnerObject;

        [SetUp]
        public void SetUp()
        {
            statsObject = new GameObject("Colonist Stats Test");
            stats = statsObject.AddComponent<ColonistStatsComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(statsObject);
            if (runnerObject != null)
                Object.DestroyImmediate(runnerObject);
        }

        [Test]
        public void BaselineFatigueAccumulatesOverGameHours()
        {
            SetFatigue(20f);

            stats.SimulationTick(2f);

            Assert.That(stats.Fatigue, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void FatigueCannotFallBelowZero()
        {
            SetFatigue(3f);

            stats.AdjustFatigue(-10f);

            Assert.That(stats.Fatigue, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FatigueMayExceedOneHundred()
        {
            SetFatigue(95f);

            stats.AdjustFatigue(30f);

            Assert.That(stats.Fatigue, Is.EqualTo(125f).Within(0.0001f));
        }

        [Test]
        public void SleepyThresholdUsesSeventyPoints()
        {
            SetFatigue(69f);
            Assert.That(stats.IsSleepy, Is.False);

            SetFatigue(70f);
            Assert.That(stats.IsSleepy, Is.True);
        }

        [Test]
        public void ExhaustionThresholdUsesOneHundredPoints()
        {
            SetFatigue(99f);
            Assert.That(stats.IsExhausted, Is.False);

            SetFatigue(100f);
            Assert.That(stats.IsExhausted, Is.True);

            SetFatigue(130f);
            Assert.That(stats.IsExhausted, Is.True);
        }

        [Test]
        public void InvalidValuesCannotPoisonFatigueOrRate()
        {
            SetFatigue(20f);

            stats.AdjustFatigue(float.NaN);
            stats.AdjustFatigue(float.PositiveInfinity);
            stats.AdjustFatigue(float.NegativeInfinity);

            Assert.That(stats.Fatigue, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.EffectiveFatiguePerGameHour, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void BindingIsNotActiveBeforeActivation()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: false, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
        }

        [Test]
        public void ActiveSleepIsExposed()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: true, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.True);
            Assert.That(runner.ActiveActivityBinding, Is.SameAs(sleep));
            Assert.That(runner.ActiveActivityId, Is.EqualTo("Sleep"));
        }

        [Test]
        public void ExitingSleepIsNotExposedAsActive()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: true, exitInProgress: true);

            Assert.That(runner.CurrentActivityId, Is.EqualTo("Sleep"));
            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
            Assert.That(runner.ActiveActivityId, Is.Null);
        }

        [Test]
        public void ActiveSleepUsesAuthoredFatigueRate()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: true, exitInProgress: false);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void SleepEntryUsesBaselineFatigueRate()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: false, exitInProgress: false);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(55f).Within(0.0001f));
        }

        [Test]
        public void SleepExitUsesBaselineFatigueRateImmediately()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: true, exitInProgress: true);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(55f).Within(0.0001f));
        }

        private ColonistActivityRunner CreateRunner()
        {
            runnerObject = new GameObject("Colonist Activity Runner Test");
            return runnerObject.AddComponent<ColonistActivityRunner>();
        }

        private void CreateRunnerAndStats()
        {
            runnerObject = new GameObject("Colonist Activity Stats Test");
            runnerObject.AddComponent<ColonistActivityRunner>();
            stats = runnerObject.AddComponent<ColonistStatsComponent>();
        }

        private static FacilityActivityBinding CreateSleepBinding()
        {
            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "Sleep");
            SetPrivateField(binding, "overridesFatigueRate", true);
            SetPrivateField(binding, "fatiguePerGameHour", -10f);
            return binding;
        }

        private static void SetRunnerState(
            ColonistActivityRunner runner,
            FacilityActivityBinding binding,
            bool activityActive,
            bool exitInProgress)
        {
            SetPrivateField(runner, "currentBinding", binding);
            SetPrivateField(runner, "activityActive", activityActive);
            SetPrivateField(runner, "exitInProgress", exitInProgress);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }

        private void SetFatigue(float value)
        {
            FieldInfo fatigue = typeof(ColonistStatsComponent).GetField(
                "fatigue", BindingFlags.Instance | BindingFlags.NonPublic);
            fatigue.SetValue(stats, value);
        }
    }
}
