using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistBrainTests
    {
        private GameObject colonistObject;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);
        }

        [Test]
        public void NotSleepyRemainsIdle()
        {
            ColonistBrain brain = CreateBrain(0f);

            brain.SimulationTick(1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void MissingSleepTargetRemainsIdle()
        {
            ColonistBrain brain = CreateBrain(70f);

            brain.SimulationTick(1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        private ColonistBrain CreateBrain(float fatigue)
        {
            colonistObject = new GameObject("Colonist Brain Test");
            ColonistStatsComponent stats =
                colonistObject.AddComponent<ColonistStatsComponent>();
            colonistObject.AddComponent<ColonistAssignments>();
            ColonistTargetResolver targetResolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            ColonistActivityRunner activityRunner =
                colonistObject.AddComponent<ColonistActivityRunner>();
            ColonistBrain brain = colonistObject.AddComponent<ColonistBrain>();

            SetPrivateField(stats, "fatigue", fatigue);
            SetPrivateField(brain, "stats", stats);
            SetPrivateField(brain, "targetResolver", targetResolver);
            SetPrivateField(brain, "activityRunner", activityRunner);
            return brain;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
