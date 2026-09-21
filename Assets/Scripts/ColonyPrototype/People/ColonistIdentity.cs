using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class ColonistIdentity : MonoBehaviour
    {
        [SerializeField]
        private string displayName = "Colonist";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? "Colonist"
                : displayName;
    }
}
