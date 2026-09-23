using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ShuttleFlightGuidanceTests
    {
        private ShuttleFlightProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<ShuttleFlightProfile>();
            profile.mainAcceleration = 8f;
            profile.rcsAcceleration = 2f;
            profile.maxCruiseSpeed = 25f;
            profile.approachMaxSpeed = 3f;
            profile.integrationSubstepSeconds = 0.1f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void MainBurnIsWithheldUntilBodyForwardIsAligned()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            Vector3 acceleration = ShuttleFlightGuidance.CalculateCruiseAcceleration(
                state, Vector3.right, new Vector3(20f, 0f, 0f), profile, out bool mainEngineFiring);

            Assert.That(mainEngineFiring, Is.False);
            Assert.That(acceleration.magnitude, Is.EqualTo(profile.rcsAcceleration).Within(0.001f));
        }

        [Test]
        public void MainBurnIsAllowedWhenBodyForwardIsAligned()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            Vector3 acceleration = ShuttleFlightGuidance.CalculateCruiseAcceleration(
                state, Vector3.forward, Vector3.zero, profile, out bool mainEngineFiring);

            Assert.That(mainEngineFiring, Is.True);
            Assert.That(acceleration.z, Is.EqualTo(profile.mainAcceleration).Within(0.001f));
        }

        [Test]
        public void RcsCommandCannotExceedHalfOfMainAcceleration()
        {
            profile.rcsAcceleration = 20f;

            Vector3 acceleration = ShuttleFlightGuidance.LimitRcsAcceleration(new Vector3(20f, 0f, 0f), profile);

            Assert.That(acceleration.magnitude, Is.EqualTo(4f).Within(0.001f));
            Assert.That(acceleration.magnitude, Is.LessThan(profile.mainAcceleration));
        }

        [Test]
        public void CruiseAccelerationCoastsAtMaximumSpeed()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.forward * profile.maxCruiseSpeed, Quaternion.identity, Vector3.zero);

            Vector3 acceleration = ShuttleFlightGuidance.CalculateCruiseAcceleration(
                state, Vector3.forward, Vector3.forward, profile, out bool mainEngineFiring);

            Assert.That(mainEngineFiring, Is.False);
            Assert.That(acceleration, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RcsSettleReducesVelocityAndReachesTargetUsingAcceleration()
        {
            Vector3 target = new Vector3(0.2f, 0f, 0f);
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, new Vector3(3f, 0f, 0f), Quaternion.identity, Vector3.zero);
            Vector3 firstAcceleration = ShuttleFlightGuidance.CalculateRcsSettleAcceleration(
                state.position, state.velocity, target, 3f, profile.rcsAcceleration, 0.05f, 0.05f, 0.1f);
            Assert.That(firstAcceleration.x, Is.LessThan(0f));
            Assert.That(firstAcceleration.magnitude, Is.LessThanOrEqualTo(profile.rcsAcceleration + 0.001f));

            for (int i = 0; i < 1500; i++)
            {
                Vector3 acceleration = ShuttleFlightGuidance.CalculateRcsSettleAcceleration(
                    state.position, state.velocity, target, 3f, profile.rcsAcceleration, 0.05f, 0.05f, 0.1f);
                ShuttleFlightIntegrator.StepSubstep(ref state, profile, acceleration, Vector3.zero, 0.1f);
            }

            Assert.That(Vector3.Distance(state.position, target), Is.LessThanOrEqualTo(0.06f));
            Assert.That(state.velocity.magnitude, Is.LessThanOrEqualTo(0.06f));
        }

        [Test]
        public void AngularGuidanceSettlesAnArbitraryThreeDimensionalOrientation()
        {
            Quaternion target = Quaternion.Euler(27f, 137f, -51f);
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.Euler(-45f, 15f, 98f), Vector3.zero);

            for (int i = 0; i < 3000; i++)
            {
                Vector3 angularAcceleration = ShuttleFlightGuidance.CalculateAngularAcceleration(
                    state, target, profile, 0.1f, out float ignoredError);
                ShuttleFlightIntegrator.StepSubstep(ref state, profile, Vector3.zero, angularAcceleration, 0.1f);
            }

            Assert.That(Quaternion.Angle(state.rotation, target), Is.LessThanOrEqualTo(profile.angleTolerance + 0.1f));
            Assert.That(state.angularVelocity.magnitude, Is.LessThanOrEqualTo(0.1f));
        }

        [Test]
        public void RouteAcceptsMultipleIntermediateCruiseWaypoints()
        {
            FlightRoute route = new FlightRoute();
            Assert.That(route.AddWaypoint(FlightWaypoint.Clearance(Vector3.zero)), Is.True);
            Assert.That(route.AddWaypoint(FlightWaypoint.Cruise(new Vector3(10f, 2f, 5f))), Is.True);
            Assert.That(route.AddWaypoint(FlightWaypoint.Cruise(new Vector3(30f, -4f, 12f))), Is.True);
            Assert.That(route.AddWaypoint(FlightWaypoint.Cruise(new Vector3(70f, 6f, 20f))), Is.True);
            Assert.That(route.AddWaypoint(FlightWaypoint.Approach(new Vector3(100f, 0f, 0f))), Is.True);

            Assert.That(route.IsValid, Is.True);
            Assert.That(route.Count, Is.EqualTo(5));
        }

        [Test]
        public void DirectSprintARouteContainsClearanceAndApproachOnly()
        {
            FlightRoute route = FlightRoute.CreateDirect(
                new Vector3(0f, 1f, 4f), new Vector3(100f, 2f, -8f), Quaternion.Euler(12f, 25f, 4f));

            Assert.That(route.IsValid, Is.True);
            Assert.That(route.Count, Is.EqualTo(2));
            Assert.That(route[0].kind, Is.EqualTo(FlightWaypointKind.Clearance));
            Assert.That(route[1].kind, Is.EqualTo(FlightWaypointKind.Approach));
            Assert.That(route[1].hasDesiredOrientation, Is.True);
        }

        [Test]
        public void CruiseWaypointProgressDoesNotRequireAFullStop()
        {
            FlightWaypoint cruise = FlightWaypoint.Cruise(Vector3.zero, 2f);
            FlightWaypoint approach = FlightWaypoint.Approach(Vector3.zero, null, 2f, 1f);

            Assert.That(FlightRoute.CanAdvancePastWaypoint(Vector3.zero, Vector3.forward * 20f, cruise), Is.True);
            Assert.That(FlightRoute.CanAdvancePastWaypoint(Vector3.zero, Vector3.forward * 20f, approach), Is.False);
        }

        [Test]
        public void LinearAndAngularStoppingHelpersUseBoundedAcceleration()
        {
            Assert.That(ShuttleFlightGuidance.StoppingDistance(10f, 2f), Is.EqualTo(25f).Within(0.001f));
            Assert.That(ShuttleFlightGuidance.StoppingAngle(90f, 30f), Is.EqualTo(135f).Within(0.001f));
            Assert.That(ShuttleFlightGuidance.EstimateRotationTime(180f, 0f, 60f, 90f),
                Is.EqualTo(3.5f).Within(0.001f));
        }
    }
}
