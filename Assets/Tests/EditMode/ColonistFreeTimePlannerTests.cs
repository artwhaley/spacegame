using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistFreeTimePlannerTests
    {
        private GameObject colonistObject;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);
        }

        [Test]
        public void BiologicalHeadroomConstrainsFreeTimeWithoutWork()
        {
            colonistObject = new GameObject("Planner Colonist");
            ColonistStatsComponent stats = colonistObject.AddComponent<ColonistStatsComponent>();
            SetPrivateField(stats, "fatigue", 0f);
            SetPrivateField(stats, "hunger", 0f);
            SetPrivateField(stats, "baselineFatiguePerGameHour", 5f);
            SetPrivateField(stats, "baselineHungerPerGameHour", 8f);

            ColonistFreeTimePlan plan = ColonistFreeTimePlanner.Calculate(stats, 0f);

            Assert.That(plan.HasUpcomingWork, Is.False);
            Assert.That(plan.MaximumSafeDiscretionaryDuration, Is.EqualTo(7.5f).Within(0.01f));
            Assert.That(plan.Reason, Is.EqualTo("safe_discretionary_budget"));
        }

        [Test]
        public void RestPreferredThresholdProducesNoDiscretionaryBudget()
        {
            colonistObject = new GameObject("Tired Planner Colonist");
            ColonistStatsComponent stats = colonistObject.AddComponent<ColonistStatsComponent>();
            SetPrivateField(stats, "fatigue", 60f);
            SetPrivateField(stats, "hunger", 0f);

            ColonistFreeTimePlan plan = ColonistFreeTimePlanner.Calculate(stats, 0f);

            Assert.That(plan.MaximumSafeDiscretionaryDuration, Is.EqualTo(0f));
            Assert.That(plan.Reason, Is.EqualTo("rest_preferred_threshold"));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
