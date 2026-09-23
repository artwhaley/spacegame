using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Reusable strategic routing clearance and bounded-search tuning.</summary>
    [CreateAssetMenu(menuName = "Spacegame/Flight/Shuttle Navigation Profile", fileName = "ShuttleNavigationProfile")]
    public sealed class ShuttleNavigationProfile : ScriptableObject
    {
        [Header("Navigation envelope")]
        [Min(0.01f)] public float navigationRadius = 2f;
        [Min(0f)] public float preferredClearance = 1f;
        [Min(0f)] public float emergencyClearance = 0.25f;
        [Min(0f)] public float detourPadding = 0.5f;
        public LayerMask navigationObstacleLayers = ~0;

        [Header("Bounded visibility search")]
        [Range(1, 12)] public int maxRelevantObstacles = 8;
        [Range(1, 8)] public int maxExpansionRounds = 4;
        [Range(4, 16)] public int maxCandidatesPerObstacle = 16;
        [Range(16, 200)] public int maxGraphNodes = 130;
        [Range(64, 12000)] public int maxEdgeTests = 10000;

        [Header("Corner speed policy")]
        [Range(0f, 90f)] public float straightTurnDegrees = 20f;
        [Range(0f, 120f)] public float moderateTurnDegrees = 60f;
        [Range(0f, 170f)] public float sharpTurnDegrees = 110f;
        [Range(0.05f, 1f)] public float moderateSpeedMultiplier = 0.8f;
        [Range(0.05f, 1f)] public float sharpSpeedMultiplier = 0.55f;
        [Range(0.05f, 1f)] public float hairpinSpeedMultiplier = 0.3f;
        [Min(0f)] public float cornerLookaheadDistance = 20f;
        [Min(0f)] public float replanCooldownSeconds = 2f;

        public float SweptRadius => Mathf.Max(0.01f, navigationRadius) + Mathf.Max(0f, preferredClearance);

        private void OnValidate()
        {
            navigationRadius = Mathf.Max(0.01f, Finite(navigationRadius, 2f));
            preferredClearance = Mathf.Max(0f, Finite(preferredClearance, 1f));
            emergencyClearance = Mathf.Max(0f, Finite(emergencyClearance, 0.25f));
            detourPadding = Mathf.Max(0f, Finite(detourPadding, 0.5f));
            maxRelevantObstacles = Mathf.Clamp(maxRelevantObstacles, 1, 12);
            maxExpansionRounds = Mathf.Clamp(maxExpansionRounds, 1, 8);
            maxCandidatesPerObstacle = Mathf.Clamp(maxCandidatesPerObstacle, 4, 16);
            maxGraphNodes = Mathf.Clamp(maxGraphNodes, 16, 200);
            maxEdgeTests = Mathf.Clamp(maxEdgeTests, 64, 12000);
            straightTurnDegrees = Mathf.Clamp(Finite(straightTurnDegrees, 20f), 0f, 90f);
            moderateTurnDegrees = Mathf.Clamp(Finite(moderateTurnDegrees, 60f), straightTurnDegrees, 120f);
            sharpTurnDegrees = Mathf.Clamp(Finite(sharpTurnDegrees, 110f), moderateTurnDegrees, 170f);
            moderateSpeedMultiplier = Mathf.Clamp(Finite(moderateSpeedMultiplier, 0.8f), 0.05f, 1f);
            sharpSpeedMultiplier = Mathf.Clamp(Finite(sharpSpeedMultiplier, 0.55f), 0.05f, 1f);
            hairpinSpeedMultiplier = Mathf.Clamp(Finite(hairpinSpeedMultiplier, 0.3f), 0.05f, 1f);
            cornerLookaheadDistance = Mathf.Max(0f, Finite(cornerLookaheadDistance, 20f));
            replanCooldownSeconds = Mathf.Max(0f, Finite(replanCooldownSeconds, 2f));
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
