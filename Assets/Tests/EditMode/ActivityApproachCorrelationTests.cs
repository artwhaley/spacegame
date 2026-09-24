using System;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public sealed class ActivityApproachCorrelationTests
    {
        private GameObject actorObject;
        private GameObject facilityObject;

        [TearDown]
        public void TearDown()
        {
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
        }

        [Test]
        public void ActivityWaitsForMatchingWholeApproachRouteCompletion()
        {
            ColonistActivityRunner runner = CreateActivityRunner(out ColonistMotor motor);
            InteractableFacility facility = CreateFacility();
            var router = new FakeApproachRouter();
            runner.SetApproachRouter(router);

            Assert.That(runner.RequestActivity(facility, "TestActivity"), Is.True);
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Navigating));

            // Multiple motor arrivals can be intermediate route legs. They must
            // not authorize facility entry.
            RaiseMotorArrived(motor);
            RaiseMotorArrived(motor);
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Navigating));
            Assert.That(runner.CurrentReservation, Is.Not.Null);

            router.Complete("stale-route-id");
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Navigating));
            Assert.That(runner.CurrentReservation, Is.Not.Null);

            // This test object has no baked NavMesh, so the matching completion
            // advances into entry and then fails the motor's NavMesh precondition.
            router.Complete(router.StartedRouteId);
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Failed));
            Assert.That(runner.CurrentReservation, Is.Null);
        }

        [Test]
        public void StaleApproachRouteFailureDoesNotFailNewActivityRequest()
        {
            ColonistActivityRunner runner = CreateActivityRunner(out _);
            InteractableFacility facility = CreateFacility();
            var router = new FakeApproachRouter();
            runner.SetApproachRouter(router);

            Assert.That(runner.RequestActivity(facility, "TestActivity"), Is.True);
            router.Fail("stale-route-id", "old route failed");

            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Navigating));
            Assert.That(runner.CurrentReservation, Is.Not.Null);

            router.Fail(router.StartedRouteId, "matching route failed");
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Failed));
            Assert.That(runner.CurrentReservation, Is.Null);
        }

        private ColonistActivityRunner CreateActivityRunner(out ColonistMotor motor)
        {
            actorObject = new GameObject("Activity Approach Correlation Actor");
            ColonistActivityRunner runner = actorObject.AddComponent<ColonistActivityRunner>();
            motor = actorObject.GetComponent<ColonistMotor>();
            return runner;
        }

        private InteractableFacility CreateFacility()
        {
            facilityObject = new GameObject("Activity Approach Correlation Facility");
            InteractableFacility facility = facilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Approach").transform;
            approach.SetParent(facilityObject.transform, false);

            var binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "TestActivity");
            SetPrivateField(binding, "reservationGroup", "TestReservation");
            SetPrivateField(binding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { binding });
            return facility;
        }

        private static void RaiseMotorArrived(ColonistMotor motor)
        {
            FieldInfo eventField = typeof(ColonistMotor).GetField(
                "Arrived",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(eventField, Is.Not.Null);
            (eventField.GetValue(motor) as Action)?.Invoke();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }

        private sealed class FakeApproachRouter : IActivityApproachRouter
        {
            public string StartedRouteId { get; private set; }
            public event Action<string> ApproachRouteCompleted;
            public event Action<string, string> ApproachRouteFailed;

            public bool TryStartRoute(
                Transform destination,
                out string routeId,
                out string failureReason)
            {
                routeId = "activity-route-1";
                StartedRouteId = routeId;
                failureReason = string.Empty;
                return destination != null;
            }

            public void StopRoute(string routeId)
            {
            }

            public void Complete(string routeId) =>
                ApproachRouteCompleted?.Invoke(routeId);

            public void Fail(string routeId, string reason) =>
                ApproachRouteFailed?.Invoke(routeId, reason);
        }
    }
}
