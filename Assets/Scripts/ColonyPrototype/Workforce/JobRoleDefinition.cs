using UnityEngine;

namespace AsteroidColony
{
    [CreateAssetMenu(
        menuName = "Asteroid Colony/Workforce/Job Role",
        fileName = "JobRole")]
    public sealed class JobRoleDefinition : ScriptableObject
    {
        [SerializeField]
        private string stableId;

        [SerializeField]
        private string displayName;

        public string StableId => stableId;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(stableId);

        private void OnValidate()
        {
            if (stableId != null)
                stableId = stableId.Trim();

            if (displayName != null)
                displayName = displayName.Trim();
        }
    }
}
