using UnityEngine;

namespace AsteroidColony
{
    public enum ResourceQuantityMode
    {
        Fractional,
        Discrete
    }

    /// <summary>
    /// Authoritative content identity and quantity semantics for one resource.
    /// StableId is the save/data identity; displayName is presentation only.
    /// </summary>
    [CreateAssetMenu(menuName = "Asteroid Colony/Resource Definition", fileName = "ResourceDefinition")]
    public class ResourceDefinition : ScriptableObject
    {
        [Tooltip("Stable content identity used by future saves and data references.")]
        public string stableId;
        public string displayName;
        [TextArea]
        public string description;
        public ResourceQuantityMode quantityMode = ResourceQuantityMode.Fractional;

        [Header("Meal (one whole unit per interaction)")]
        [Min(0f)] public float hungerRecoveryPerUnit;
        [Min(0f)] public float consumptionDurationGameHours;

        public bool IsDiscrete => quantityMode == ResourceQuantityMode.Discrete;
        public bool IsEdible => IsDiscrete &&
            IsPositiveFinite(hungerRecoveryPerUnit) &&
            IsPositiveFinite(consumptionDurationGameHours);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
            if (float.IsNaN(hungerRecoveryPerUnit) || float.IsInfinity(hungerRecoveryPerUnit))
                hungerRecoveryPerUnit = 0f;
            if (float.IsNaN(consumptionDurationGameHours) || float.IsInfinity(consumptionDurationGameHours))
                consumptionDurationGameHours = 0f;
            hungerRecoveryPerUnit = Mathf.Max(0f, hungerRecoveryPerUnit);
            consumptionDurationGameHours = Mathf.Max(0f, consumptionDurationGameHours);
        }
    }
}
