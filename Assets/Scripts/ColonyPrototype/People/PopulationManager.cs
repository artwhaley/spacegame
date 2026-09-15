using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Registry of all colonists. Job roles and workplace assignments are configured
    /// manually in the Inspector; no employment allocator is implemented.
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [SerializeField] private List<ColonistAgent> colonists = new List<ColonistAgent>();

        public IReadOnlyList<ColonistAgent> Colonists => colonists;
        public int TotalPopulation => colonists.Count;

        private void Awake()
        {
            Instance = this;
        }

        public void Register(ColonistAgent colonist)
        {
            if (colonist != null && !colonists.Contains(colonist))
                colonists.Add(colonist);
        }

        public void Unregister(ColonistAgent colonist)
        {
            if (colonist != null)
                colonists.Remove(colonist);
        }

        public int CountResidents(LocationAnchor homeLocation)
        {
            int count = 0;
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i].home == homeLocation)
                    count++;
            return count;
        }
    }
}