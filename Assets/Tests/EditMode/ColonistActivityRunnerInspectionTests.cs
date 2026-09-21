using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistActivityRunnerInspectionTests
    {
        private GameObject runnerObject;
        private GameObject facilityObject;

        [TearDown]
        public void TearDown()
        {
            if (runnerObject != null)
                Object.DestroyImmediate(runnerObject);
            if (facilityObject != null)
                Object.DestroyImmediate(facilityObject);
        }

        [Test]
        public void CurrentReservationGroupIsReadOnlyAndClearsAfterRelease()
        {
            runnerObject = new GameObject("Colonist Activity Runner");
            ColonistActivityRunner runner = runnerObject.AddComponent<ColonistActivityRunner>();
            facilityObject = new GameObject("Facility");
            InteractableFacility facility = facilityObject.AddComponent<InteractableFacility>();

            Assert.That(runner.CurrentReservationGroup, Is.Null);
            Assert.That(
                facility.TryAcquire("Sleep", runner, out FacilityReservationToken token),
                Is.True);

            SetPrivateField(runner, "currentFacility", facility);
            SetPrivateField(runner, "reservation", token);

            Assert.That(runner.CurrentReservationGroup, Is.EqualTo("Sleep"));
            Assert.That(facility.Release(token), Is.True);
            Assert.That(runner.CurrentReservationGroup, Is.Null);
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
