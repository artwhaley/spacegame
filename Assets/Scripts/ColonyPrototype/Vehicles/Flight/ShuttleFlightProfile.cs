using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Reusable acceleration, speed, and capture tuning for a Shuttle.</summary>
    [CreateAssetMenu(menuName = "Spacegame/Flight/Shuttle Flight Profile", fileName = "ShuttleFlightProfile")]
    public sealed class ShuttleFlightProfile : ScriptableObject
    {
        [Header("Translation")]
        [Min(0.01f)] public float mainAcceleration = 8f;
        [Min(0.01f)] public float rcsAcceleration = 2f;
        [Min(0.1f)] public float maxCruiseSpeed = 25f;
        [Min(0.01f)] public float approachMaxSpeed = 3f;
        [Min(0.01f)] public float finalDockMaxSpeed = 0.5f;

        [Header("Rotation")]
        [Min(0.01f)] public float angularAcceleration = 90f;
        [Min(0.01f)] public float maxAngularSpeed = 90f;
        [Range(0f, 180f)] public float mainBurnAlignmentDegrees = 8f;

        [Header("Guidance and capture")]
        [Min(0f)] public float positionTolerance = 0.2f;
        [Min(0f)] public float velocityTolerance = 0.1f;
        [Min(0f)] public float angleTolerance = 1f;
        [Min(0f)] public float captureAngularSpeedTolerance = 2f;
        [Min(0.001f)] public float integrationSubstepSeconds = 0.1f;
        [Min(0f)] public float brakingSafetyMargin = 5f;

        private void OnValidate()
        {
            mainAcceleration = Positive(mainAcceleration, 8f);
            rcsAcceleration = Positive(rcsAcceleration, 2f);
            maxCruiseSpeed = Positive(maxCruiseSpeed, 25f);
            approachMaxSpeed = Positive(approachMaxSpeed, 3f);
            finalDockMaxSpeed = Positive(finalDockMaxSpeed, 0.5f);
            angularAcceleration = Positive(angularAcceleration, 90f);
            maxAngularSpeed = Positive(maxAngularSpeed, 90f);
            mainBurnAlignmentDegrees = Mathf.Clamp(Finite(mainBurnAlignmentDegrees, 8f), 0f, 180f);
            positionTolerance = NonNegative(positionTolerance, 0.2f);
            velocityTolerance = NonNegative(velocityTolerance, 0.1f);
            angleTolerance = NonNegative(angleTolerance, 1f);
            captureAngularSpeedTolerance = NonNegative(captureAngularSpeedTolerance, 2f);
            integrationSubstepSeconds = Positive(integrationSubstepSeconds, 0.1f);
            brakingSafetyMargin = NonNegative(brakingSafetyMargin, 5f);
        }

        private static float Positive(float value, float fallback)
        {
            return Mathf.Max(0.001f, NonNegative(value, fallback));
        }

        private static float NonNegative(float value, float fallback)
        {
            return Mathf.Max(0f, Finite(value, fallback));
        }

        private static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
