using System;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Deterministic, bounded kinematic integration. Inputs are world-space
    /// accelerations; time is simulated seconds, never render-frame time.
    /// </summary>
    public static class ShuttleFlightIntegrator
    {
        private const float MinUsefulSeconds = 0.000001f;
        private const float MinAcceleration = 0.000001f;

        public static void Step(ref ShuttleFlightState state, ShuttleFlightProfile profile,
            Vector3 linearAcceleration, Vector3 angularAcceleration, float elapsedSeconds)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
                return;

            float maxSubstep = IsFinite(profile.integrationSubstepSeconds)
                ? Mathf.Max(0.001f, profile.integrationSubstepSeconds)
                : 0.1f;
            double elapsed = elapsedSeconds;
            int fullSteps = (int)Math.Floor(elapsed / maxSubstep + 1e-10d);
            double remainder = elapsed - fullSteps * (double)maxSubstep;

            for (int i = 0; i < fullSteps; i++)
                IntegrateSubstep(ref state, profile, linearAcceleration, angularAcceleration, maxSubstep);

            if (remainder > MinUsefulSeconds)
                IntegrateSubstep(ref state, profile, linearAcceleration, angularAcceleration, (float)remainder);
        }

        /// <summary>Runs one internal integration substep for feedback controllers.</summary>
        public static void StepSubstep(ref ShuttleFlightState state, ShuttleFlightProfile profile,
            Vector3 linearAcceleration, Vector3 angularAcceleration, float elapsedSeconds)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
                return;
            IntegrateSubstep(ref state, profile, linearAcceleration, angularAcceleration, elapsedSeconds);
        }

        public static Quaternion Normalize(Quaternion value)
        {
            float magnitudeSquared = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (!IsFinite(magnitudeSquared) || magnitudeSquared <= MinAcceleration)
                return Quaternion.identity;
            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            return new Quaternion(value.x * inverseMagnitude, value.y * inverseMagnitude,
                value.z * inverseMagnitude, value.w * inverseMagnitude);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void IntegrateSubstep(ref ShuttleFlightState state, ShuttleFlightProfile profile,
            Vector3 requestedLinearAcceleration, Vector3 requestedAngularAcceleration, float dt)
        {
            if (!state.IsFinite)
            {
                state = new ShuttleFlightState(
                    IsFinite(state.position) ? state.position : Vector3.zero,
                    IsFinite(state.velocity) ? state.velocity : Vector3.zero,
                    IsFinite(state.rotation) ? state.rotation : Quaternion.identity,
                    IsFinite(state.angularVelocity) ? state.angularVelocity : Vector3.zero);
            }

            float mainAcceleration = SafePositive(profile.mainAcceleration, 8f);
            float maxSpeed = SafePositive(profile.maxCruiseSpeed, 25f);
            float maxAngularAcceleration = SafePositive(profile.angularAcceleration, 90f);
            float maxAngularSpeed = SafePositive(profile.maxAngularSpeed, 90f);
            Vector3 acceleration = ClampMagnitude(SafeVector(requestedLinearAcceleration), mainAcceleration);
            Vector3 angularAcceleration = ClampMagnitude(SafeVector(requestedAngularAcceleration), maxAngularAcceleration);

            IntegrateTranslation(ref state, acceleration, dt, maxSpeed);
            IntegrateRotation(ref state, angularAcceleration, dt, maxAngularSpeed);
            state.lastLinearAcceleration = acceleration;
            state.lastAngularAcceleration = angularAcceleration;
        }

        private static void IntegrateTranslation(ref ShuttleFlightState state, Vector3 acceleration,
            float dt, float maxSpeed)
        {
            Vector3 startVelocity = ClampMagnitude(state.velocity, maxSpeed);
            Vector3 candidateVelocity = startVelocity + acceleration * dt;
            float capSquared = maxSpeed * maxSpeed;

            if (candidateVelocity.sqrMagnitude <= capSquared || acceleration.sqrMagnitude <= MinAcceleration)
            {
                state.position += startVelocity * dt + 0.5f * acceleration * dt * dt;
                state.velocity = ClampMagnitude(candidateVelocity, maxSpeed);
                return;
            }

            float speedSquaredBefore = startVelocity.sqrMagnitude;
            float alongAcceleration = Vector3.Dot(startVelocity, acceleration);
            float accelerationSquared = acceleration.sqrMagnitude;
            float discriminant = alongAcceleration * alongAcceleration -
                accelerationSquared * (speedSquaredBefore - capSquared);
            float hitTime = 0f;
            if (discriminant >= 0f)
            {
                hitTime = (-alongAcceleration + Mathf.Sqrt(discriminant)) / accelerationSquared;
                hitTime = Mathf.Clamp(hitTime, 0f, dt);
            }

            if (hitTime > MinUsefulSeconds)
            {
                state.position += startVelocity * hitTime +
                    0.5f * acceleration * hitTime * hitTime;
                Vector3 atCap = ClampMagnitude(startVelocity + acceleration * hitTime, maxSpeed);
                float coastTime = dt - hitTime;
                state.position += atCap * coastTime;
                state.velocity = atCap;
            }
            else
            {
                Vector3 cappedVelocity = ClampMagnitude(candidateVelocity, maxSpeed);
                state.position += 0.5f * (startVelocity + cappedVelocity) * dt;
                state.velocity = cappedVelocity;
            }
        }

        private static void IntegrateRotation(ref ShuttleFlightState state, Vector3 acceleration,
            float dt, float maxAngularSpeed)
        {
            Vector3 startVelocity = ClampMagnitude(state.angularVelocity, maxAngularSpeed);
            Vector3 endVelocity = ClampMagnitude(startVelocity + acceleration * dt, maxAngularSpeed);
            Vector3 averageVelocity = 0.5f * (startVelocity + endVelocity);
            float angle = averageVelocity.magnitude * dt;

            if (angle > MinUsefulSeconds && averageVelocity.sqrMagnitude > MinAcceleration)
            {
                Quaternion delta = Quaternion.AngleAxis(angle, averageVelocity.normalized);
                state.rotation = Normalize(delta * state.rotation);
            }

            state.angularVelocity = endVelocity;
        }

        private static Vector3 SafeVector(Vector3 value)
        {
            return IsFinite(value) ? value : Vector3.zero;
        }

        private static float SafePositive(float value, float fallback)
        {
            return IsFinite(value) ? Mathf.Max(MinAcceleration, value) : fallback;
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            float maxSquared = maxMagnitude * maxMagnitude;
            float magnitudeSquared = value.sqrMagnitude;
            if (magnitudeSquared <= maxSquared || magnitudeSquared <= MinAcceleration)
                return value;
            return value * (maxMagnitude / Mathf.Sqrt(magnitudeSquared));
        }
    }
}
