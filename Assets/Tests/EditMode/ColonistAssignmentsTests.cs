using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistAssignmentsTests
    {
        private GameObject assignmentsObject;
        private GameObject facilityObject;

        [TearDown]
        public void TearDown()
        {
            if (assignmentsObject != null)
                Object.DestroyImmediate(assignmentsObject);

            if (facilityObject != null)
                Object.DestroyImmediate(facilityObject);
        }

        [Test]
        public void UnconfiguredSleepTargetFailsCleanly()
        {
            ColonistAssignments assignments = CreateAssignments();

            bool result = assignments.TryGetSleepTarget(out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.IsConfigured, Is.False);
        }

        [Test]
        public void ActivityTargetReportsItsExactTarget()
        {
            InteractableFacility facility = CreateFacility();
            ActivityTarget target = CreateTarget(facility, "Sleep");

            Assert.That(target.Facility, Is.SameAs(facility));
            Assert.That(target.ActivityId, Is.EqualTo("Sleep"));
            Assert.That(target.IsConfigured, Is.True);
        }

        [Test]
        public void AssignmentReturnsExactConfiguredTarget()
        {
            InteractableFacility facility = CreateFacility();
            ActivityTarget target = CreateTarget(facility, "Sleep");
            ColonistAssignments assignments = CreateAssignments();
            SetPrivateField(assignments, "sleepTarget", target);

            bool result = assignments.TryGetSleepTarget(out ActivityTarget returnedTarget);

            Assert.That(result, Is.True);
            Assert.That(returnedTarget, Is.SameAs(target));
        }

        [Test]
        public void MissingFacilityOrActivityIdIsNotConfigured()
        {
            InteractableFacility facility = CreateFacility();
            ActivityTarget missingActivityId = CreateTarget(facility, string.Empty);
            ActivityTarget missingFacility = CreateTarget(null, "Sleep");

            Assert.That(missingActivityId.IsConfigured, Is.False);
            Assert.That(missingFacility.IsConfigured, Is.False);
        }

        private ColonistAssignments CreateAssignments()
        {
            assignmentsObject = new GameObject("Colonist Assignments Test");
            return assignmentsObject.AddComponent<ColonistAssignments>();
        }

        private InteractableFacility CreateFacility()
        {
            facilityObject = new GameObject("CommandPod Test");
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
