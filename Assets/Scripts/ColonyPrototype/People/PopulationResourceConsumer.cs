using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [System.Serializable]
    public class PopulationConsumptionEntry
    {
        public ResourceDefinition resource;
        public float amountPerResidentPerGameHour;
        [SerializeField] private bool shortageActive;
        [SerializeField] private float requestedLastTick;
        [SerializeField] private float consumedLastTick;

        public bool ShortageActive => shortageActive;
        public float RequestedLastTick => requestedLastTick;
        public float ConsumedLastTick => consumedLastTick;

        public bool Validate()
        {
            if (resource == null)
                return false;
            if (amountPerResidentPerGameHour < 0f || float.IsNaN(amountPerResidentPerGameHour) ||
                float.IsInfinity(amountPerResidentPerGameHour))
                return false;
            return !resource.IsDiscrete || ResourceQuantityRules.IsWhole(amountPerResidentPerGameHour);
        }

        public void Consume(float amount, InventoryComponent inventory, string locationName)
        {
            requestedLastTick = Mathf.Max(0f, amount);
            consumedLastTick = inventory != null ? inventory.Remove(resource, requestedLastTick) : 0f;
            bool shortageNow = consumedLastTick < requestedLastTick - 0.0001f;
            if (shortageNow && !shortageActive)
                SimulationLog.Log($"{resource.displayName} shortage at {locationName} - inventory empty");
            else if (!shortageNow && shortageActive)
                SimulationLog.Log($"{resource.displayName} shortage at {locationName} resolved");
            shortageActive = shortageNow;
        }
    }

    /// <summary>
    /// Consumes configured resources from a habitation inventory for its current
    /// resident count. It does not create personal inventories or health gameplay.
    /// </summary>
    public class PopulationResourceConsumer : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public HabitationComponent habitation;
        public InventoryComponent inventory;
        public List<PopulationConsumptionEntry> entries = new List<PopulationConsumptionEntry>();

        public int ResidentCount => habitation != null ? habitation.ResidentCount : 0;
        public IReadOnlyList<PopulationConsumptionEntry> Entries => entries;
        public int SimulationTickPriority => 200;

        private void Awake()
        {
            if (habitation == null)
                habitation = GetComponent<HabitationComponent>();
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            string locationName = habitation != null && habitation.location != null
                ? habitation.location.displayName
                : name;
            int residents = ResidentCount;
            for (int i = 0; i < entries.Count; i++)
            {
                PopulationConsumptionEntry entry = entries[i];
                if (entry == null || !entry.Validate())
                    continue;
                entry.Consume(residents * entry.amountPerResidentPerGameHour * Mathf.Max(0f, deltaGameHours),
                    inventory, locationName);
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                PopulationConsumptionEntry entry = entries[i];
                if (entry != null && !entry.Validate() && entry.resource != null)
                    Debug.LogError($"Invalid population consumption rate for {entry.resource.name}.", this);
            }
        }
    }
}
