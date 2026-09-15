using UnityEngine;

namespace AsteroidColony
{
    [CreateAssetMenu(menuName = "Asteroid Colony/Worker Class Definition", fileName = "WorkerClassDefinition")]
    public class WorkerClassDefinition : ScriptableObject
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
