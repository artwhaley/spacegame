using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Registry of all colonists. Job roles and workplace assignments are configured
    /// explicitly; <see cref="StaffingManager"/> executes them and there is still no
    /// automatic employment allocator.
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [SerializeField] private List<ColonistAgent> colonists = new List<ColonistAgent>();

        public IReadOnlyList<ColonistAgent> Colonists => colonists;
        public int TotalPopulation => colonists.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active PopulationManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
            DiscoverColonists();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;
            DiscoverColonists();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void DiscoverColonists()
        {
            ColonistAgent[] found = FindObjectsByType<ColonistAgent>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
                Register(found[i]);
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
            for (int i = colonists.Count - 1; i >= 0; i--)
                if (colonists[i] == null)
                    colonists.RemoveAt(i);
            int count = 0;
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i].home == homeLocation)
                    count++;
            return count;
        }
    }
}
