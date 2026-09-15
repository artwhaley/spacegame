using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public class InventoryEntry
    {
        public ResourceDefinition resource;
        public float onHand;
        public float reserved;
        public float capacity;

        [SerializeField] private float available;

        /// <summary>On Hand minus Reserved. Maintained by InventoryComponent.</summary>
        public float Available => available;

        public void Refresh()
        {
            if (resource != null)
            {
                if (resource.IsDiscrete)
                {
                    if (!ResourceQuantityRules.TryNormalize(resource, onHand, out onHand))
                        Debug.LogError($"Inventory entry {resource.name} has invalid discrete OnHand quantity.");
                    if (!ResourceQuantityRules.TryNormalize(resource, reserved, out reserved))
                        Debug.LogError($"Inventory entry {resource.name} has invalid discrete Reserved quantity.");
                    if (!ResourceQuantityRules.TryNormalize(resource, capacity, out capacity))
                        Debug.LogError($"Inventory entry {resource.name} has invalid discrete Capacity quantity.");
                }

                onHand = Mathf.Max(0f, onHand);
                capacity = Mathf.Max(0f, capacity);
                if (onHand > capacity)
                    onHand = capacity;
                reserved = Mathf.Clamp(reserved, 0f, onHand);
                available = Mathf.Max(0f, onHand - reserved);
                return;
            }

            onHand = Mathf.Max(0f, onHand);
            capacity = Mathf.Max(0f, capacity);
            if (onHand > capacity)
                onHand = capacity;
            reserved = Mathf.Clamp(reserved, 0f, onHand);
            available = Mathf.Max(0f, onHand - reserved);
        }
    }

    /// <summary>
    /// Local inventory owned by a single facility or vehicle.
    /// Invariants: On Hand >= 0, Reserved >= 0, Reserved <= On Hand, On Hand <= Capacity.
    /// No resource is created or destroyed here except through explicit production
    /// or consumption calls from other systems.
    /// </summary>
    public class InventoryComponent : MonoBehaviour
    {
        [SerializeField] private List<InventoryEntry> entries = new List<InventoryEntry>();

        public IReadOnlyList<InventoryEntry> Entries => entries;

        public InventoryEntry GetEntry(ResourceDefinition resource)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].resource == resource)
                    return entries[i];
            return null;
        }

        private InventoryEntry GetOrCreate(ResourceDefinition resource)
        {
            if (resource == null)
                return null;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
            {
                entry = new InventoryEntry { resource = resource };
                entries.Add(entry);
            }
            return entry;
        }

        public float GetOnHand(ResourceDefinition resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.onHand : 0f;
        }

        public float GetAvailable(ResourceDefinition resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            return entry.Available;
        }

        public float GetReserved(ResourceDefinition resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.reserved : 0f;
        }

        public float GetFreeCapacity(ResourceDefinition resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            return Mathf.Max(0f, entry.capacity - entry.onHand);
        }

        public float GetCapacity(ResourceDefinition resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.capacity : 0f;
        }

        /// <summary>Adds stock up to capacity. Returns the amount actually added.</summary>
        public float Add(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return 0f;
            InventoryEntry entry = GetOrCreate(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            float space = Mathf.Max(0f, entry.capacity - entry.onHand);
            float added = Mathf.Min(normalized, space);
            if (resource.IsDiscrete)
                added = Mathf.Floor(added + ResourceQuantityRules.WholeNumberEpsilon);
            entry.onHand += added;
            entry.Refresh();
            return added;
        }

        /// <summary>Returns true when the resource can accept at least this amount.</summary>
        public bool HasFreeCapacity(ResourceDefinition resource, float amount = 0f)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized))
                return false;
            return GetFreeCapacity(resource) >= Mathf.Max(0f, normalized);
        }

        /// <summary>
        /// Removes stock (consumption). Returns the amount actually removed.
        /// If removal dips below reserved stock, reservations are reduced to match.
        /// </summary>
        public float Remove(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return 0f;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null || entry.onHand <= 0f)
                return 0f;
            entry.Refresh();
            float removed = Mathf.Min(normalized, entry.onHand);
            if (resource.IsDiscrete)
                removed = Mathf.Floor(removed + ResourceQuantityRules.WholeNumberEpsilon);
            entry.onHand -= removed;
            if (entry.reserved > entry.onHand)
                entry.reserved = entry.onHand;
            entry.Refresh();
            return removed;
        }

        /// <summary>Reserves available stock. Returns false if insufficient available stock.</summary>
        public bool Reserve(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized))
                return false;
            if (normalized <= 0f)
                return true;
            InventoryEntry entry = GetOrCreate(resource);
            if (entry == null)
                return false;
            entry.Refresh();
            if (entry.Available < normalized)
                return false;
            entry.reserved += normalized;
            entry.Refresh();
            return true;
        }

        /// <summary>Releases a previous reservation back to available stock.</summary>
        public void ReleaseReservation(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return;
            entry.Refresh();
            entry.reserved = Mathf.Max(0f, entry.reserved - normalized);
            entry.Refresh();
        }

        /// <summary>Physically withdraws previously reserved stock. Returns the amount actually withdrawn.</summary>
        public float WithdrawReserved(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return 0f;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            float withdrawable = Mathf.Min(entry.reserved, entry.onHand);
            float withdrawn = Mathf.Min(normalized, withdrawable);
            if (resource.IsDiscrete)
                withdrawn = Mathf.Floor(withdrawn + ResourceQuantityRules.WholeNumberEpsilon);
            entry.reserved -= withdrawn;
            entry.onHand -= withdrawn;
            entry.Refresh();
            return withdrawn;
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    entries[i].Refresh();
        }

        private static bool TryNormalizeMutation(ResourceDefinition resource, float amount, out float normalized)
        {
            if (ResourceQuantityRules.TryNormalize(resource, amount, out normalized))
                return true;

            if (resource != null)
                Debug.LogError($"Rejected invalid {resource.name} inventory quantity: {amount}.");
            return false;
        }
    }
}
