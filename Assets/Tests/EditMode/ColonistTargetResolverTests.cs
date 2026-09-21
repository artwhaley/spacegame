using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistTargetResolverTests
    {
        private GameObject colonistObject;
        private GameObject facilityObject;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);

            if (facilityObject != null)
                Object.DestroyImmediate(facilityObject);
        }

        [Test]
        public void SleepResolvesExactPersonalAssignment()
        {
            ColonistAssignments assignments = CreateAssignments();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            InteractableFacility facility = CreateFacility();
            ActivityTarget expected = CreateTarget(facility, "Sleep");
            SetPrivateField(assignments, "sleepTarget", expected);

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.True);
            Assert.That(target, Is.SameAs(expected));
            Assert.That(target.Facility, Is.SameAs(facility));
            Assert.That(target.ActivityId, Is.EqualTo("Sleep"));
        }

        [Test]
        public void MissingSleepAssignmentReturnsFalseAndNull()
        {
            CreateAssignments();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void ResolverWithoutAssignmentsFailsCleanly()
        {
            colonistObject = new GameObject("Resolver Without Assignments Test");
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        private ColonistAssignments CreateAssignments()
        {
            colonistObject = new GameObject("Colonist Target Resolver Test");
            return colonistObject.AddComponent<ColonistAssignments>();
        }

        private InteractableFacility CreateFacility()
        {
            facilityObject = new GameObject("Assigned Facility Test");
            return facilityObject.AddComponent<InteractableFacility>();
        }

        private static ActivityTarget CreateTarget(
            InteractableFacility facility,
            string activityId)
        {
            ActivityTarget target = new ActivityTarget();
            SetPrivateField(target, "facility", facility);
            SetPrivateField(target, "activityId", activityId);
            return target;
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
