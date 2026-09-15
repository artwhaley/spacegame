using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Single source of truth for the transport priority scale.</summary>
    public static class TransportPriorityRules
    {
        public const int Minimum = 1;
        public const int Maximum = 10;

        public static int Clamp(int priority)
        {
            return Mathf.Clamp(priority, Minimum, Maximum);
        }
    }
}
