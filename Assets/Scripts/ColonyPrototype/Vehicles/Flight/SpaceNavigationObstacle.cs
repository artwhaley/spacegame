using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Explicitly authored strategic obstacle; decorative colliders are ignored.</summary>
    [DisallowMultipleComponent]
    public sealed class SpaceNavigationObstacle : MonoBehaviour
    {
        private static readonly HashSet<SpaceNavigationObstacle> Active = new HashSet<SpaceNavigationObstacle>();
        [SerializeField] private string stableId;
        [SerializeField] private Vector3 centerOffset;
        [Min(0f)] [SerializeField] private float avoidanceRadiusOverride;
        [SerializeField] private bool isDynamic;
        [SerializeField] private bool showNavigationGizmo = true;
        private float cachedDerivedRadius;

        public string StableId => string.IsNullOrWhiteSpace(stableId) ? HierarchyKey(transform) : stableId.Trim();
        internal string StableHierarchyPath => HierarchyKey(transform);
        public bool IsDynamic => isDynamic;
        public Vector3 ConservativeCenter => transform.TransformPoint(centerOffset);
        public float AvoidanceRadius => avoidanceRadiusOverride > 0f ? avoidanceRadiusOverride : cachedDerivedRadius;
        internal static IEnumerable<SpaceNavigationObstacle> ActiveObstacles => Active;

        private void OnEnable()
        {
            CacheDerivedRadius();
            Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void OnDestroy() => Active.Remove(this);

        private void CacheDerivedRadius()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            float radius = 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].isTrigger)
                    continue;
                Bounds bounds = colliders[i].bounds;
                Vector3 ext = bounds.extents;
                float corner = ext.magnitude + Vector3.Distance(ConservativeCenter, bounds.center);
                radius = Mathf.Max(radius, corner);
            }
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                Bounds bounds = renderers[i].bounds;
                radius = Mathf.Max(radius, bounds.extents.magnitude +
                    Vector3.Distance(ConservativeCenter, bounds.center));
            }
            cachedDerivedRadius = Mathf.Max(0.01f, radius);
        }

        private static string HierarchyKey(Transform node)
        {
            string key = string.Empty;
            while (node != null)
            {
                key = node.GetSiblingIndex().ToString("D4") + ":" + node.name +
                    (string.IsNullOrEmpty(key) ? string.Empty : "/" + key);
                node = node.parent;
            }
            return key;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showNavigationGizmo)
                return;
            Gizmos.color = isDynamic ? new Color(1f, 0.65f, 0.1f, 0.8f) : new Color(1f, 0.25f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(ConservativeCenter, AvoidanceRadius);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            avoidanceRadiusOverride = Mathf.Max(0f, float.IsNaN(avoidanceRadiusOverride) ||
                float.IsInfinity(avoidanceRadiusOverride) ? 0f : avoidanceRadiusOverride);
            if (!Application.isPlaying)
                CacheDerivedRadius();
        }
#endif
    }
}
