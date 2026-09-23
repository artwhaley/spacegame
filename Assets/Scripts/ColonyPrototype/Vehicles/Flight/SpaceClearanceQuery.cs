using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Central, filtered Unity-physics and conservative-envelope queries for shuttle routing.</summary>
    public static class SpaceClearanceQuery
    {
        private static readonly RaycastHit[] CastHits = new RaycastHit[512];
        private static readonly Collider[] OverlapHits = new Collider[512];

        public static bool IsSweptSegmentClear(Vector3 start, Vector3 end, float radius,
            Transform ignoredRoot, out SpaceNavigationObstacle blocker, int layerMask = ~0)
        {
            blocker = FindFirstBlocker(start, end, radius, ignoredRoot, layerMask);
            return blocker == null;
        }

        public static SpaceNavigationObstacle FindFirstBlocker(Vector3 start, Vector3 end,
            float radius, Transform ignoredRoot, int layerMask = ~0)
        {
            if (!ShuttleFlightIntegrator.IsFinite(start) || !ShuttleFlightIntegrator.IsFinite(end) ||
                !ShuttleFlightIntegrator.IsFinite(radius) || radius < 0f)
                return null;

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            float bestFraction = float.PositiveInfinity;
            SpaceNavigationObstacle best = null;
            foreach (SpaceNavigationObstacle obstacle in SpaceNavigationObstacle.ActiveObstacles)
            {
                if (!IsRelevant(obstacle, ignoredRoot, layerMask))
                    continue;
                if (SegmentSphereFraction(start, end, obstacle.ConservativeCenter,
                        radius + obstacle.AvoidanceRadius, out float fraction) &&
                    (fraction < bestFraction - 0.00001f ||
                     (Mathf.Abs(fraction - bestFraction) <= 0.00001f && Compare(obstacle, best) < 0)))
                {
                    bestFraction = fraction;
                    best = obstacle;
                }
            }

            // The marked obstacles' conservative spheres cover their geometry. This physics sweep
            // keeps collider filtering centralized and catches authoring mistakes in that bound.
            int count = distance > 0.0001f
                ? Physics.SphereCastNonAlloc(start, radius, delta / distance, CastHits, distance,
                    layerMask, QueryTriggerInteraction.Ignore)
                : 0;
            if (count >= CastHits.Length)
                return best;
            for (int i = 0; i < count; i++)
            {
                Collider collider = CastHits[i].collider;
                if (collider == null || IsIgnored(collider.transform, ignoredRoot))
                    continue;
                SpaceNavigationObstacle obstacle = collider.GetComponentInParent<SpaceNavigationObstacle>();
                if (obstacle == null || !IsRelevant(obstacle, ignoredRoot, layerMask))
                    continue;
                float fraction = distance > 0f ? Mathf.Clamp01(CastHits[i].distance / distance) : 0f;
                if (fraction < bestFraction - 0.00001f ||
                    (Mathf.Abs(fraction - bestFraction) <= 0.00001f && Compare(obstacle, best) < 0))
                {
                    bestFraction = fraction;
                    best = obstacle;
                }
            }
            return best;
        }

        public static void CollectBlockingObstacles(Vector3 start, Vector3 end, float radius,
            Transform ignoredRoot, List<SpaceNavigationObstacle> results, int layerMask = ~0)
        {
            if (results == null)
                return;
            results.Clear();
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            float best = float.PositiveInfinity;
            SpaceNavigationObstacle first = null;
            foreach (SpaceNavigationObstacle obstacle in SpaceNavigationObstacle.ActiveObstacles)
            {
                if (!IsRelevant(obstacle, ignoredRoot, layerMask) ||
                    !SegmentSphereFraction(start, end, obstacle.ConservativeCenter,
                        Mathf.Max(0f, radius) + obstacle.AvoidanceRadius, out float fraction))
                    continue;
                if (!results.Contains(obstacle))
                    results.Add(obstacle);
                if (fraction < best - 0.00001f ||
                    (Mathf.Abs(fraction - best) <= 0.00001f && Compare(obstacle, first) < 0))
                {
                    best = fraction;
                    first = obstacle;
                }
            }
            int count = distance > 0.0001f
                ? Physics.SphereCastNonAlloc(start, Mathf.Max(0f, radius), delta / distance,
                    CastHits, distance, layerMask, QueryTriggerInteraction.Ignore)
                : 0;
            for (int i = 0; i < count && i < CastHits.Length; i++)
            {
                Collider collider = CastHits[i].collider;
                if (collider == null || IsIgnored(collider.transform, ignoredRoot))
                    continue;
                SpaceNavigationObstacle obstacle = collider.GetComponentInParent<SpaceNavigationObstacle>();
                if (obstacle != null && IsRelevant(obstacle, ignoredRoot, layerMask) && !results.Contains(obstacle))
                    results.Add(obstacle);
            }
            if (first != null && !results.Contains(first))
                results.Add(first);
            results.Sort(Compare);
        }

        public static bool IsVolumeClear(Vector3 position, float radius, Transform ignoredRoot,
            out SpaceNavigationObstacle blocker, int layerMask = ~0)
        {
            blocker = null;
            if (!ShuttleFlightIntegrator.IsFinite(position) || !ShuttleFlightIntegrator.IsFinite(radius) || radius < 0f)
                return false;
            foreach (SpaceNavigationObstacle obstacle in SpaceNavigationObstacle.ActiveObstacles)
            {
                if (IsRelevant(obstacle, ignoredRoot, layerMask) &&
                    Vector3.Distance(position, obstacle.ConservativeCenter) <= radius + obstacle.AvoidanceRadius)
                {
                    if (blocker == null || Compare(obstacle, blocker) < 0)
                        blocker = obstacle;
                }
            }
            int count = Physics.OverlapSphereNonAlloc(position, radius, OverlapHits, layerMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < OverlapHits.Length; i++)
            {
                Collider collider = OverlapHits[i];
                if (collider == null || IsIgnored(collider.transform, ignoredRoot))
                    continue;
                SpaceNavigationObstacle obstacle = collider.GetComponentInParent<SpaceNavigationObstacle>();
                if (obstacle != null && IsRelevant(obstacle, ignoredRoot, layerMask) &&
                    (blocker == null || Compare(obstacle, blocker) < 0))
                    blocker = obstacle;
            }
            return blocker == null;
        }

        private static bool IsRelevant(SpaceNavigationObstacle obstacle, Transform ignoredRoot, int layerMask) =>
            obstacle != null && obstacle.isActiveAndEnabled &&
            (layerMask & (1 << obstacle.gameObject.layer)) != 0 && !IsIgnored(obstacle.transform, ignoredRoot);

        private static bool IsIgnored(Transform candidate, Transform root) =>
            root != null && (candidate == root || candidate.IsChildOf(root));

        private static int Compare(SpaceNavigationObstacle a, SpaceNavigationObstacle b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int key = string.CompareOrdinal(a.StableId, b.StableId);
            if (key != 0) return key;
            key = string.CompareOrdinal(a.StableHierarchyPath, b.StableHierarchyPath);
            if (key != 0) return key;
            Vector3 ac = a.ConservativeCenter;
            Vector3 bc = b.ConservativeCenter;
            int x = ac.x.CompareTo(bc.x); if (x != 0) return x;
            int y = ac.y.CompareTo(bc.y); if (y != 0) return y;
            int z = ac.z.CompareTo(bc.z); if (z != 0) return z;
            return a.AvoidanceRadius.CompareTo(b.AvoidanceRadius);
        }

        private static bool SegmentSphereFraction(Vector3 start, Vector3 end, Vector3 center,
            float combinedRadius, out float fraction)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float radiusSquared = combinedRadius * combinedRadius;
            if (lengthSquared <= 0.0000001f)
            {
                fraction = 0f;
                return (start - center).sqrMagnitude <= radiusSquared;
            }
            Vector3 offset = start - center;
            float c = offset.sqrMagnitude - radiusSquared;
            if (c <= 0f)
            {
                fraction = 0f;
                return true;
            }
            float b = Vector3.Dot(offset, segment);
            float discriminant = b * b - lengthSquared * c;
            if (discriminant < 0f)
            {
                fraction = 0f;
                return false;
            }
            float t = (-b - Mathf.Sqrt(discriminant)) / lengthSquared;
            fraction = t;
            return t >= 0f && t <= 1f;
        }
    }
}
