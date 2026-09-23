using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Stateless maneuver helpers. Guidance chooses bounded acceleration;
    /// ShuttleFlightIntegrator applies it.
    /// </summary>
    public static class ShuttleFlightGuidance
    {
        private const float DirectionEpsilon = 0.000001f;

        public static bool IsMainBurnAligned(ShuttleFlightState state, Vector3 desiredBurnDirection,
            ShuttleFlightProfile profile)
        {
            if (profile == null || !ShuttleFlightIntegrator.IsFinite(desiredBurnDirection) ||
                desiredBurnDirection.sqrMagnitude <= DirectionEpsilon)
                return false;
            Vector3 forward = state.rotation * Vector3.forward;
            return Vector3.Angle(forward, desiredBurnDirection.normalized) <=
                Mathf.Clamp(profile.mainBurnAlignmentDegrees, 0f, 180f);
        }

        /// <summary>Main thrust is available only along authored body-forward.</summary>
        public static bool TryGetMainEngineAcceleration(ShuttleFlightState state,
            Vector3 desiredBurnDirection, ShuttleFlightProfile profile, out Vector3 acceleration)
        {
            acceleration = Vector3.zero;
            if (!IsMainBurnAligned(state, desiredBurnDirection, profile))
                return false;
            acceleration = (state.rotation * Vector3.forward).normalized * Mathf.Max(0.001f, profile.mainAcceleration);
            return true;
        }

        /// <summary>Uses RCS alone while aligning, and coasts at the cruise-speed cap.</summary>
        public static Vector3 CalculateCruiseAcceleration(ShuttleFlightState state,
            Vector3 desiredBurnDirection, Vector3 rcsCorrection, ShuttleFlightProfile profile,
            out bool mainEngineFiring)
        {
            mainEngineFiring = false;
            if (profile == null || state.velocity.magnitude >= Mathf.Max(0.001f, profile.maxCruiseSpeed) - 0.001f)
                return Vector3.zero;
            if (TryGetMainEngineAcceleration(state, desiredBurnDirection, profile, out Vector3 main))
            {
                mainEngineFiring = true;
                return main;
            }
            return LimitRcsAcceleration(rcsCorrection, profile);
        }

        public static Vector3 LimitRcsAcceleration(Vector3 requestedAcceleration, ShuttleFlightProfile profile)
        {
            if (profile == null || !ShuttleFlightIntegrator.IsFinite(requestedAcceleration))
                return Vector3.zero;
            float mainCap = Mathf.Max(0.001f, profile.mainAcceleration);
            float rcsCap = Mathf.Clamp(profile.rcsAcceleration, 0.001f, mainCap * 0.5f);
            return Vector3.ClampMagnitude(requestedAcceleration, rcsCap);
        }

        /// <summary>Returns world-space angular acceleration toward a target orientation.</summary>
        public static Vector3 CalculateAngularAcceleration(ShuttleFlightState state,
            Quaternion desiredRotation, ShuttleFlightProfile profile, float elapsedSeconds,
            out float angularErrorDegrees)
        {
            angularErrorDegrees = 0f;
            if (profile == null || !ShuttleFlightIntegrator.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f ||
                !ShuttleFlightIntegrator.IsFinite(desiredRotation))
                return Vector3.zero;

            GetShortestRotationError(state.rotation, desiredRotation, out Vector3 axis, out angularErrorDegrees);
            float accelerationLimit = Mathf.Max(0.001f, profile.angularAcceleration);
            float speedLimit = Mathf.Max(0.001f, profile.maxAngularSpeed);
            float remainingAngle = Mathf.Max(0f, angularErrorDegrees - Mathf.Max(0f, profile.angleTolerance));
            float desiredSpeed = Mathf.Min(speedLimit, Mathf.Sqrt(2f * accelerationLimit * remainingAngle));
            Vector3 desiredAngularVelocity = remainingAngle <= 0.001f ? Vector3.zero : axis * desiredSpeed;
            Vector3 acceleration = (desiredAngularVelocity - state.angularVelocity) / elapsedSeconds;
            return Vector3.ClampMagnitude(acceleration, accelerationLimit);
        }

        /// <summary>
        /// RCS velocity servo with a stopping-limited desired speed. The command
        /// is acceleration, never a position interpolation.
        /// </summary>
        public static Vector3 CalculateRcsSettleAcceleration(Vector3 position, Vector3 velocity,
            Vector3 targetPosition, float maxSpeed, float maxAcceleration,
            float positionTolerance, float velocityTolerance, float elapsedSeconds)
        {
            if (!ShuttleFlightIntegrator.IsFinite(position) || !ShuttleFlightIntegrator.IsFinite(velocity) ||
                !ShuttleFlightIntegrator.IsFinite(targetPosition) || !ShuttleFlightIntegrator.IsFinite(elapsedSeconds) ||
                elapsedSeconds <= 0f)
                return Vector3.zero;

            maxSpeed = Mathf.Max(0.001f, maxSpeed);
            maxAcceleration = Mathf.Max(0.001f, maxAcceleration);
            Vector3 error = targetPosition - position;
            float distance = error.magnitude;
            if (distance <= Mathf.Max(0f, positionTolerance) && velocity.magnitude <= Mathf.Max(0f, velocityTolerance))
                return Vector3.zero;

            Vector3 desiredVelocity = Vector3.zero;
            if (distance > DirectionEpsilon)
            {
                float stoppingLimitedSpeed = Mathf.Sqrt(2f * maxAcceleration * distance);
                desiredVelocity = error / distance * Mathf.Min(maxSpeed, stoppingLimitedSpeed);
            }

            Vector3 acceleration = (desiredVelocity - velocity) / elapsedSeconds;
            return Vector3.ClampMagnitude(acceleration, maxAcceleration);
        }

        /// <summary>Orient body-forward toward a burn vector without guessing world axes.</summary>
        public static Quaternion RotationForForward(Vector3 forward, Vector3 preferredUp)
        {
            if (!ShuttleFlightIntegrator.IsFinite(forward) || forward.sqrMagnitude <= DirectionEpsilon)
                return Quaternion.identity;

            forward.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(preferredUp, forward);
            if (!ShuttleFlightIntegrator.IsFinite(up) || up.sqrMagnitude <= DirectionEpsilon)
                up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude <= DirectionEpsilon)
                up = Vector3.ProjectOnPlane(Vector3.right, forward);
            return Quaternion.LookRotation(forward, up.normalized);
        }

        public static float StoppingDistance(float speed, float acceleration)
        {
            speed = ShuttleFlightIntegrator.IsFinite(speed) ? Mathf.Max(0f, speed) : 0f;
            acceleration = ShuttleFlightIntegrator.IsFinite(acceleration) ? Mathf.Max(0f, acceleration) : 0f;
            return acceleration <= DirectionEpsilon ? float.PositiveInfinity : speed * speed / (2f * acceleration);
        }

        /// <summary>Stopping angle uses degrees, matching the profile's angular units.</summary>
        public static float StoppingAngle(float angularSpeedDegreesPerSecond, float angularAccelerationDegreesPerSecondSquared)
        {
            return StoppingDistance(angularSpeedDegreesPerSecond, angularAccelerationDegreesPerSecondSquared);
        }

        /// <summary>Conservative rest-to-rest angular time plus the current-rate allowance.</summary>
        public static float EstimateRotationTime(float angleDegrees, float currentAngularSpeed,
            float angularAcceleration, float maxAngularSpeed)
        {
            angleDegrees = ShuttleFlightIntegrator.IsFinite(angleDegrees) ? Mathf.Max(0f, angleDegrees) : 0f;
            currentAngularSpeed = ShuttleFlightIntegrator.IsFinite(currentAngularSpeed)
                ? Mathf.Max(0f, currentAngularSpeed) : 0f;
            angularAcceleration = ShuttleFlightIntegrator.IsFinite(angularAcceleration)
                ? Mathf.Max(0.001f, angularAcceleration) : 0.001f;
            maxAngularSpeed = ShuttleFlightIntegrator.IsFinite(maxAngularSpeed)
                ? Mathf.Max(0.001f, maxAngularSpeed) : 0.001f;

            float fullSpeedThreshold = maxAngularSpeed * maxAngularSpeed / angularAcceleration;
            float restToRestTime = angleDegrees <= fullSpeedThreshold
                ? 2f * Mathf.Sqrt(angleDegrees / angularAcceleration)
                : 2f * maxAngularSpeed / angularAcceleration +
                    (angleDegrees - fullSpeedThreshold) / maxAngularSpeed;
            return restToRestTime + currentAngularSpeed / angularAcceleration;
        }

        private static void GetShortestRotationError(Quaternion current, Quaternion desired,
            out Vector3 axis, out float angleDegrees)
        {
            Quaternion error = ShuttleFlightIntegrator.Normalize(desired * Quaternion.Inverse(current));
            error.ToAngleAxis(out angleDegrees, out axis);
            if (!ShuttleFlightIntegrator.IsFinite(angleDegrees) ||
                !ShuttleFlightIntegrator.IsFinite(axis) || axis.sqrMagnitude <= DirectionEpsilon)
            {
                axis = Vector3.up;
                angleDegrees = 0f;
                return;
            }
            if (angleDegrees > 180f)
            {
                angleDegrees = 360f - angleDegrees;
                axis = -axis;
            }
            axis.Normalize();
        }
    }
}
