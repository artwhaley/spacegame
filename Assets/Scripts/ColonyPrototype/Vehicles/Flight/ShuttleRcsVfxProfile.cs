using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Presentation-only tuning for short RCS exhaust jets.</summary>
    [CreateAssetMenu(menuName = "Spacegame/Flight/Shuttle RCS VFX Profile", fileName = "ShuttleRcsVfxProfile")]
    public sealed class ShuttleRcsVfxProfile : ScriptableObject
    {
        [Header("Short exhaust jet")]
        [Min(0.001f)] public float puffSize = 0.055f;
        [Range(0f, 0.9f)] public float puffSizeVariation = 0.2f;
        [Min(0.01f)] public float puffLifetime = 0.085f;
        [Min(0f)] public float exhaustSpeed = 5f;
        [Min(1)] public int particlesPerBurst = 18;
        [Range(0f, 90f)] public float spreadDegrees = 7f;
        [Range(0f, 1f)] public float puffOpacity = 0.85f;
        [Range(0f, 1f)] public float minimumDirectionAlignment = 0.55f;
        [Min(0f)] public float nozzleExitOffset = 0.035f;

        private void OnValidate()
        {
            puffSize = Mathf.Max(0.001f, Safe(puffSize, 0.055f));
            puffSizeVariation = Mathf.Clamp(Safe(puffSizeVariation, 0.2f), 0f, 0.9f);
            puffLifetime = Mathf.Max(0.01f, Safe(puffLifetime, 0.085f));
            exhaustSpeed = Mathf.Max(0f, Safe(exhaustSpeed, 5f));
            particlesPerBurst = Mathf.Clamp(particlesPerBurst, 1, 64);
            spreadDegrees = Mathf.Clamp(Safe(spreadDegrees, 7f), 0f, 89f);
            puffOpacity = Mathf.Clamp01(Safe(puffOpacity, 0.85f));
            minimumDirectionAlignment = Mathf.Clamp01(Safe(minimumDirectionAlignment, 0.55f));
            nozzleExitOffset = Mathf.Max(0f, Safe(nozzleExitOffset, 0.035f));
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
