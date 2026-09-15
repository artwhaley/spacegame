using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public class InventoryEntry
    {
        public ResourceType resource;
        public float onHand;
        public float reserved;
        public float capacity;

        [SerializeField] private float available;

        /// <summary>On Hand minus Reserved. Maintained by InventoryComponent.</summary>
        public float Available => available;

        public void Refresh()
        {
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

        public InventoryEntry GetEntry(ResourceType resource)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].resource == resource)
                    return entries[i];
            return null;
        }

        private InventoryEntry GetOrCreate(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
            {
                entry = new InventoryEntry { resource = resource };
                entries.Add(entry);
            }
            return entry;
        }

        public float GetOnHand(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.onHand : 0f;
        }

        public float GetAvailable(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            return entry.Available;
        }

        public float GetReserved(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.reserved : 0f;
        }

        public float GetFreeCapacity(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            return Mathf.Max(0f, entry.capacity - entry.onHand);
        }

        public float GetCapacity(ResourceType resource)
        {
            InventoryEntry entry = GetEntry(resource);
            if (entry != null)
                entry.Refresh();
            return entry != null ? entry.capacity : 0f;
        }

        /// <summary>Adds stock up to capacity. Returns the amount actually added.</summary>
        public float Add(ResourceType resource, float amount)
        {
            if (amount <= 0f)
                return 0f;
            InventoryEntry entry = GetOrCreate(resource);
            entry.Refresh();
            float space = Mathf.Max(0f, entry.capacity - entry.onHand);
            float added = Mathf.Min(amount, space);
            entry.onHand += added;
            entry.Refresh();
            return added;
        }

        /// <summary>Returns true when the resource can accept at least this amount.</summary>
        public bool HasFreeCapacity(ResourceType resource, float amount = 0f)
        {
            return GetFreeCapacity(resource) >= Mathf.Max(0f, amount);
        }

        /// <summary>
        /// Removes stock (consumption). Returns the amount actually removed.
        /// If removal dips below reserved stock, reservations are reduced to match.
        /// </summary>
        public float Remove(ResourceType resource, float amount)
        {
            if (amount <= 0f)
                return 0f;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null || entry.onHand <= 0f)
                return 0f;
            entry.Refresh();
            float removed = Mathf.Min(amount, entry.onHand);
            entry.onHand -= removed;
            if (entry.reserved > entry.onHand)
                entry.reserved = entry.onHand;
            entry.Refresh();
            return removed;
        }

        /// <summary>Reserves available stock. Returns false if insufficient available stock.</summary>
        public bool Reserve(ResourceType resource, float amount)
        {
            if (amount <= 0f)
                return true;
            InventoryEntry entry = GetOrCreate(resource);
            entry.Refresh();
            if (entry.Available < amount)
                return false;
            entry.reserved += amount;
            entry.Refresh();
            return true;
        }

        /// <summary>Releases a previous reservation back to available stock.</summary>
        public void ReleaseReservation(ResourceType resource, float amount)
        {
            if (amount <= 0f)
                return;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return;
            entry.Refresh();
            entry.reserved = Mathf.Max(0f, entry.reserved - amount);
            entry.Refresh();
        }

        /// <summary>Physically withdraws previously reserved stock. Returns the amount actually withdrawn.</summary>
        public float WithdrawReserved(ResourceType resource, float amount)
        {
            if (amount <= 0f)
                return 0f;
            InventoryEntry entry = GetEntry(resource);
            if (entry == null)
                return 0f;
            entry.Refresh();
            float withdrawable = Mathf.Min(entry.reserved, entry.onHand);
            float withdrawn = Mathf.Min(amount, withdrawable);
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
    }
}
