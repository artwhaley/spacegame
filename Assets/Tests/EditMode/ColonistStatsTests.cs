using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistStatsTests
    {
        private GameObject statsObject;
        private ColonistStatsComponent stats;

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
            stats.SetFatigueRateOverride(-10f);

            stats.SimulationTick(1f);

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
        public void FatigueRateOverrideReplacesBaseline()
        {
            SetFatigue(50f);
            stats.SetFatigueRateOverride(-10f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void ClearingFatigueRateOverrideRestoresBaseline()
        {
            SetFatigue(50f);
            stats.SetFatigueRateOverride(-10f);
            stats.SimulationTick(1f);

            stats.ClearFatigueRateOverride();
            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(45f).Within(0.0001f));
        }

        [Test]
        public void InvalidValuesCannotPoisonFatigueOrRate()
        {
            SetFatigue(20f);

            stats.AdjustFatigue(float.NaN);
            stats.AdjustFatigue(float.PositiveInfinity);
            stats.AdjustFatigue(float.NegativeInfinity);
            stats.SetFatigueRateOverride(float.NaN);
            stats.SetFatigueRateOverride(float.PositiveInfinity);
            stats.SetFatigueRateOverride(float.NegativeInfinity);

            Assert.That(stats.Fatigue, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.HasFatigueRateOverride, Is.False);
            Assert.That(stats.EffectiveFatiguePerGameHour, Is.EqualTo(5f).Within(0.0001f));
        }

        private void SetFatigue(float value)
        {
            FieldInfo fatigue = typeof(ColonistStatsComponent).GetField(
                "fatigue", BindingFlags.Instance | BindingFlags.NonPublic);
            fatigue.SetValue(stats, value);
        }
    }
}
