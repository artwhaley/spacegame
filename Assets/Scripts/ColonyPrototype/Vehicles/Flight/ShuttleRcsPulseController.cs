using UnityEngine;

namespace AsteroidColony
{
    public enum ShuttleRcsActivity
    {
        Settled,
        Accelerating,
        Coasting,
        Braking
    }

    public struct ShuttleRcsPulseCommand
    {
        public Vector3 acceleration;
        public ShuttleRcsActivity activity;
        public bool started;

        public ShuttleRcsPulseCommand(Vector3 acceleration, ShuttleRcsActivity activity, bool started)
        {
            this.acceleration = acceleration;
            this.activity = activity;
            this.started = started;
        }
    }

    public struct ShuttleRcsPulseEvent
    {
        public Vector3 localLinearAcceleration;
        public Vector3 localAngularAcceleration;
        public float strength;

        public ShuttleRcsPulseEvent(Vector3 localLinearAcceleration, Vector3 localAngularAcceleration, float strength)
        {
            this.localLinearAcceleration = localLinearAcceleration;
            this.localAngularAcceleration = localAngularAcceleration;
            this.strength = strength;
        }
    }

    /// <summary>
    /// Converts position or attitude error into short RCS accelerations separated by
    /// inertial coasts. All times are simulated seconds, independent of render rate.
    /// </summary>
    public sealed class ShuttleRcsPulseController
    {
        private float pulseRemainingSeconds;
        private float coastRemainingSeconds;
        private Vector3 pulseDirection;
        private ShuttleRcsActivity pulseActivity = ShuttleRcsActivity.Settled;
        private ShuttleRcsActivity lastPulseActivity = ShuttleRcsActivity.Settled;

        public void Reset()
        {
            pulseRemainingSeconds = 0f;
            coastRemainingSeconds = 0f;
            pulseDirection = Vector3.zero;
            pulseActivity = ShuttleRcsActivity.Settled;
            lastPulseActivity = ShuttleRcsActivity.Settled;
        }

        public ShuttleRcsPulseCommand StepLinear(Vector3 position, Vector3 velocity,
            Vector3 target, float phaseMaxSpeed, ShuttleFlightProfile profile, float dt)
        {
            float acceleration = Mathf.Clamp(profile.rcsAcceleration, 0.001f,
                Mathf.Max(0.001f, profile.mainAcceleration * 0.5f));
            float speedLimit = Mathf.Max(0.01f, Mathf.Min(phaseMaxSpeed, profile.rcsMaxCorrectionSpeed));
            float positionDeadband = Mathf.Max(0f, profile.rcsPositionDeadband);
            float velocityDeadband = Mathf.Max(0f, profile.rcsVelocityDeadband);
            Vector3 error = target - position;
            float distance = error.magnitude;
            float speed = velocity.magnitude;

            if (distance <= positionDeadband && speed <= velocityDeadband)
                return Settled();

            Vector3 toward = distance > 0.000001f ? error / distance : Vector3.zero;
            float brakePulses = Mathf.Ceil(speed / Mathf.Max(0.001f,
                acceleration * profile.rcsLinearPulseSeconds));
            float coastAllowance = speed * profile.rcsMinimumCoastSeconds * Mathf.Max(0f, brakePulses - 1f);
            float usableDistance = Mathf.Max(0f, distance - positionDeadband - coastAllowance);
            float stoppingLimitedSpeed = Mathf.Sqrt(2f * acceleration * usableDistance);
            float desiredSpeed = Mathf.Min(speedLimit, stoppingLimitedSpeed);
            Vector3 desiredVelocity = toward * desiredSpeed;
            Vector3 velocityError = desiredVelocity - velocity;
            if (velocityError.sqrMagnitude <= velocityDeadband * velocityDeadband)
                return Coast(dt, profile.rcsMinimumCoastSeconds);
            Vector3 requestedAcceleration = Vector3.ClampMagnitude(velocityError / dt, acceleration);

            bool braking = Vector3.Dot(requestedAcceleration, velocity) < 0f;
            return Advance(requestedAcceleration,
                braking ? ShuttleRcsActivity.Braking : ShuttleRcsActivity.Accelerating,
                profile.rcsLinearPulseSeconds, profile.rcsMinimumCoastSeconds, dt);
        }

        public ShuttleRcsPulseCommand StepAngular(Quaternion rotation, Vector3 angularVelocity,
            Quaternion target, ShuttleFlightProfile profile, float dt, out float errorDegrees)
        {
            ShortestRotationError(rotation, target, out Vector3 axis, out errorDegrees);
            float acceleration = Mathf.Max(0.001f, profile.angularAcceleration);
            float speedLimit = Mathf.Max(0.01f, Mathf.Min(profile.maxAngularSpeed,
                profile.rcsMaxTurnSpeed));
            float angleDeadband = Mathf.Max(0f, profile.rcsAngleDeadband);
            float rateDeadband = Mathf.Max(0f, profile.rcsAngularSpeedDeadband);
            float speed = angularVelocity.magnitude;

            if (errorDegrees <= angleDeadband && speed <= rateDeadband)
                return Settled();

            float alongSpeed = Vector3.Dot(angularVelocity, axis);
            Vector3 lateralVelocity = angularVelocity - axis * alongSpeed;
            float brakeAngle = speed * speed / (2f * acceleration);
            float brakePulses = Mathf.Ceil(speed / Mathf.Max(0.001f,
                acceleration * profile.rcsAngularPulseSeconds));
            float coastAllowance = speed * profile.rcsMinimumCoastSeconds * Mathf.Max(0f, brakePulses - 1f);
            bool shouldBrake = speed > rateDeadband &&
                (errorDegrees <= angleDeadband || alongSpeed < -rateDeadband ||
                 lateralVelocity.magnitude > Mathf.Max(rateDeadband, Mathf.Abs(alongSpeed) * 0.5f) ||
                 brakeAngle + coastAllowance >= Mathf.Max(0f, errorDegrees - angleDeadband));

            if (shouldBrake)
                return Advance(-angularVelocity.normalized * Mathf.Min(acceleration, speed / dt),
                    ShuttleRcsActivity.Braking, profile.rcsAngularPulseSeconds,
                    profile.rcsMinimumCoastSeconds, dt);
            if (alongSpeed >= speedLimit - rateDeadband)
                return Coast(dt, profile.rcsMinimumCoastSeconds);

            float launchAcceleration = Mathf.Min(acceleration,
                Mathf.Max(0f, speedLimit - alongSpeed) / dt);
            return Advance(axis * launchAcceleration, ShuttleRcsActivity.Accelerating,
                profile.rcsAngularPulseSeconds, profile.rcsMinimumCoastSeconds, dt);
        }

        private ShuttleRcsPulseCommand Advance(Vector3 requestedAcceleration,
            ShuttleRcsActivity requestedActivity, float duration, float minimumCoast, float dt)
        {
            if (requestedAcceleration.sqrMagnitude <= 0.000001f)
                return Coast(dt, minimumCoast);

            Vector3 requestedDirection = requestedAcceleration.normalized;
            if (pulseRemainingSeconds > 0f && pulseActivity == requestedActivity &&
                Vector3.Dot(pulseDirection, requestedDirection) >= 0.85f)
            {
                return ContinuePulse(requestedAcceleration, minimumCoast, dt, false);
            }

            if (pulseRemainingSeconds > 0f)
            {
                pulseRemainingSeconds = 0f;
                coastRemainingSeconds = Mathf.Max(coastRemainingSeconds, minimumCoast);
            }

            // A newly required counter-pulse may interrupt a coast. Repeated brake
            // pulses still observe the gap so braking is visibly discrete.
            if (requestedActivity == ShuttleRcsActivity.Braking &&
                lastPulseActivity != ShuttleRcsActivity.Braking)
                coastRemainingSeconds = 0f;
            if (coastRemainingSeconds > 0f)
                return Coast(dt, minimumCoast);

            pulseActivity = requestedActivity;
            lastPulseActivity = requestedActivity;
            pulseDirection = requestedDirection;
            pulseRemainingSeconds = Mathf.Max(0.001f, duration);
            return ContinuePulse(requestedAcceleration, minimumCoast, dt, true);
        }

        private ShuttleRcsPulseCommand ContinuePulse(Vector3 acceleration,
            float minimumCoast, float dt, bool started)
        {
            float fraction = Mathf.Clamp01(pulseRemainingSeconds / dt);
            pulseRemainingSeconds = Mathf.Max(0f, pulseRemainingSeconds - dt);
            if (pulseRemainingSeconds <= 0f)
                coastRemainingSeconds = Mathf.Max(0f, minimumCoast);
            return new ShuttleRcsPulseCommand(acceleration * fraction, pulseActivity, started);
        }

        private ShuttleRcsPulseCommand Coast(float dt, float minimumCoast)
        {
            if (pulseRemainingSeconds > 0f)
            {
                pulseRemainingSeconds = 0f;
                coastRemainingSeconds = Mathf.Max(coastRemainingSeconds, minimumCoast);
            }
            coastRemainingSeconds = Mathf.Max(0f, coastRemainingSeconds - dt);
            return new ShuttleRcsPulseCommand(Vector3.zero, ShuttleRcsActivity.Coasting, false);
        }

        private ShuttleRcsPulseCommand Settled()
        {
            Reset();
            return new ShuttleRcsPulseCommand(Vector3.zero, ShuttleRcsActivity.Settled, false);
        }

        private static void ShortestRotationError(Quaternion current, Quaternion target,
            out Vector3 axis, out float angleDegrees)
        {
            Quaternion error = ShuttleFlightIntegrator.Normalize(target * Quaternion.Inverse(current));
            error.ToAngleAxis(out angleDegrees, out axis);
            if (!ShuttleFlightIntegrator.IsFinite(angleDegrees) ||
                !ShuttleFlightIntegrator.IsFinite(axis) || axis.sqrMagnitude <= 0.000001f)
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
