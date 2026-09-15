using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Marks an object as a logical location that can contain people or cargo.
    /// Its transform represents the logical position of the location.
    /// Phase 1 locations: Command Post, Farm, Shuttle.
    /// </summary>
    public class LocationAnchor : MonoBehaviour
    {
        public string displayName;
    }
}