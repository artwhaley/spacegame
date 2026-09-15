using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Describes a facility's resident capacity without assigning beds.</summary>
    public class HabitationComponent : MonoBehaviour
    {
        public LocationAnchor location;
        public int capacity = 8;

        public int ResidentCount => PopulationManager.Instance != null && location != null
            ? PopulationManager.Instance.CountResidents(location)
            : 0;

        private void Awake()
        {
            if (location == null)
                location = GetComponent<LocationAnchor>();
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(0, capacity);
        }
    }
}
