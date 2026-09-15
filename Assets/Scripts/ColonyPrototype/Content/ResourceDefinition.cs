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

        public bool IsDiscrete => quantityMode == ResourceQuantityMode.Discrete;

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
        }
    }
}
