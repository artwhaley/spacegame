using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ShuttleFlightIntegratorTests
    {
        private ShuttleFlightProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<ShuttleFlightProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ZeroAccelerationPreservesVelocity()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, new Vector3(3f, -2f, 1f), Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, Vector3.zero, Vector3.zero, 1f);

            Assert.That(state.velocity.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(state.velocity.y, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(state.velocity.z, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ZeroVelocityAndAccelerationPreservePosition()
        {
            Vector3 start = new Vector3(2f, -4f, 8f);
            ShuttleFlightState state = new ShuttleFlightState(start, Vector3.zero, Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, Vector3.zero, Vector3.zero, 4f);

            Assert.That(Vector3.Distance(state.position, start), Is.LessThan(0.0001f));
        }

        [Test]
        public void ConstantAccelerationProducesExpectedVelocityAndDisplacement()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, new Vector3(2f, 0f, 0f), Vector3.zero, 2f);

            Assert.That(state.velocity.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(state.position.x, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void ReverseAccelerationReducesVelocityThroughIntegration()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, new Vector3(10f, 0f, 0f), Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, new Vector3(-2f, 0f, 0f), Vector3.zero, 1f);

            Assert.That(state.velocity.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(state.position.x, Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void SpeedCapIsAppliedAtTheCrossingTime()
        {
            profile.mainAcceleration = 10f;
            profile.maxCruiseSpeed = 5f;
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, new Vector3(10f, 0f, 0f), Vector3.zero, 1f);

            Assert.That(state.velocity.magnitude, Is.EqualTo(5f).Within(0.001f));
            Assert.That(state.position.x, Is.EqualTo(3.75f).Within(0.02f));
        }

        [Test]
        public void AngularAccelerationChangesRateAndRotation()
        {
            profile.angularAcceleration = 10f;
            profile.maxAngularSpeed = 50f;
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, Vector3.zero, new Vector3(0f, 10f, 0f), 1f);

            Assert.That(state.angularVelocity.y, Is.EqualTo(10f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, state.rotation), Is.EqualTo(5f).Within(0.02f));
        }

        [Test]
        public void AngularSpeedIsCapped()
        {
            profile.angularAcceleration = 100f;
            profile.maxAngularSpeed = 30f;
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, Vector3.zero);

            ShuttleFlightIntegrator.Step(ref state, profile, Vector3.zero, new Vector3(0f, 100f, 0f), 1f);

            Assert.That(state.angularVelocity.magnitude, Is.EqualTo(30f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, state.rotation), Is.EqualTo(15f).Within(0.05f));
        }

        [Test]
        public void QuaternionRemainsNormalizedDuringLongRun()
        {
            profile.maxCruiseSpeed = 100f;
            profile.angularAcceleration = 30f;
            profile.maxAngularSpeed = 45f;
            ShuttleFlightState state = new ShuttleFlightState(
                Vector3.zero, Vector3.zero, Quaternion.identity, new Vector3(20f, 7f, 3f));

            for (int i = 0; i < 20000; i++)
                ShuttleFlightIntegrator.Step(ref state, profile, Vector3.zero, new Vector3(1f, -0.5f, 0.25f), 0.05f);

            float norm = Mathf.Sqrt(state.rotation.x * state.rotation.x + state.rotation.y * state.rotation.y +
                state.rotation.z * state.rotation.z + state.rotation.w * state.rotation.w);
            Assert.That(state.IsFinite, Is.True);
            Assert.That(norm, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SameCommandsProduceTheSameFinalState()
        {
            ShuttleFlightState first = new ShuttleFlightState(
                new Vector3(1f, 2f, 3f), new Vector3(1f, 0f, 2f), Quaternion.Euler(3f, 14f, -5f), Vector3.zero);
            ShuttleFlightState second = first;

            for (int i = 0; i < 20; i++)
            {
                Vector3 acceleration = i < 10 ? new Vector3(1f, 0f, 3f) : new Vector3(-2f, 0f, -1f);
                Vector3 angularAcceleration = i < 10 ? new Vector3(2f, 3f, 0f) : new Vector3(-1f, 0f, 2f);
                ShuttleFlightIntegrator.Step(ref first, profile, acceleration, angularAcceleration, 0.1f);
                ShuttleFlightIntegrator.Step(ref second, profile, acceleration, angularAcceleration, 0.1f);
            }

            AssertStatesNear(first, second, 0f);
        }

        [Test]
        public void EquivalentElapsedTimeWithConstantCommandsIsGroupingIndependent()
        {
            ShuttleFlightState grouped = new ShuttleFlightState(
                Vector3.zero, new Vector3(2f, 0f, 0f), Quaternion.identity, Vector3.zero);
            ShuttleFlightState split = grouped;
            Vector3 linearAcceleration = new Vector3(1f, 0f, 0.5f);
            Vector3 angularAcceleration = new Vector3(0f, 8f, 0f);

            ShuttleFlightIntegrator.Step(ref grouped, profile, linearAcceleration, angularAcceleration, 1f);
            for (int i = 0; i < 10; i++)
                ShuttleFlightIntegrator.Step(ref split, profile, linearAcceleration, angularAcceleration, 0.1f);

            AssertStatesNear(grouped, split, 0.0002f);
        }

        [Test]
        public void NoLogicalTimeMeansNoMovement()
        {
            ShuttleFlightState state = new ShuttleFlightState(
                new Vector3(1f, 2f, 3f), new Vector3(2f, 0f, 0f), Quaternion.Euler(2f, 3f, 4f),
                new Vector3(1f, 2f, 3f));
            ShuttleFlightState before = state;

            ShuttleFlightIntegrator.Step(ref state, profile, new Vector3(8f, 0f, 0f), new Vector3(0f, 9f, 0f), 0f);

            AssertStatesNear(state, before, 0f);
        }

        private static void AssertStatesNear(ShuttleFlightState actual, ShuttleFlightState expected, float tolerance)
        {
            Assert.That(Vector3.Distance(actual.position, expected.position), Is.LessThanOrEqualTo(tolerance));
            Assert.That(Vector3.Distance(actual.velocity, expected.velocity), Is.LessThanOrEqualTo(tolerance));
            Assert.That(Quaternion.Angle(actual.rotation, expected.rotation), Is.LessThanOrEqualTo(tolerance));
            Assert.That(Vector3.Distance(actual.angularVelocity, expected.angularVelocity), Is.LessThanOrEqualTo(tolerance));
        }
    }
}
