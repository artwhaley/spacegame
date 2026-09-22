using System;
using UnityEngine;

namespace AsteroidColony.Stress
{
    /// <summary>
    /// Stable run identity for stress actors. Instance IDs are deliberately not
    /// used in manifests or digests because Unity may assign them differently
    /// between otherwise identical runs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StressStableIdentity : MonoBehaviour
    {
        [SerializeField] private string stableKey;
        [SerializeField] private int stableId;

        public string StableKey => string.IsNullOrEmpty(stableKey) ? name : stableKey;
        public int StableId => stableId != 0 ? stableId : StableHash(StableKey);

        public void Configure(string key)
        {
            stableKey = key ?? string.Empty;
            stableId = StableHash(StableKey);
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619u;
                }

                int result = (int)(hash & 0x7fffffff);
                return result == 0 ? 1 : result;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(stableKey))
                stableKey = name;
            stableId = StableHash(stableKey);
        }
    }
}
