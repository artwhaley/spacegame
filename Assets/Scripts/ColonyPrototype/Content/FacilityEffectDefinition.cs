using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// One facility performance channel, for example Production Rate or Treatment
    /// Speed. Providers publish multiplier contributions to a channel; consumers
    /// query the aggregated multiplier. All effects default to 1.0.
    /// </summary>
    [CreateAssetMenu(menuName = "Asteroid Colony/Facility Effect Definition", fileName = "FacilityEffectDefinition")]
    public class FacilityEffectDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        [TextArea]
        public string description;

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
        }
    }
}
