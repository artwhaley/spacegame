using UnityEngine;

namespace AsteroidColony
{
    [CreateAssetMenu(menuName = "Asteroid Colony/Skill Definition", fileName = "SkillDefinition")]
    public class SkillDefinition : ScriptableObject
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
