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
        private static readonly List<InventoryComponent> knownInventories = new List<InventoryComponent>();
        [SerializeField] private List<InventoryEntry> entries = new List<InventoryEntry>();
        [NonSerialized] private readonly List<InventoryReservationToken> ownedReservations =
            new List<InventoryReservationToken>();
        [NonSerialized] private readonly Dictionary<ResourceDefinition, float> legacyReservations =
            new Dictionary<ResourceDefinition, float>();

        public IReadOnlyList<InventoryEntry> Entries => entries;
        public static IReadOnlyList<InventoryComponent> Inventories => knownInventories;
        public event Action<InventoryComponent, ResourceDefinition> OnChanged;

        private void OnEnable()
        {
            if (!knownInventories.Contains(this))
                knownInventories.Add(this);
        }

        private void OnDestroy()
        {
            knownInventories.Remove(this);
        }

        public InventoryEntry GetEntry(ResourceDefinition resource)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].resource == resource)
                    return entries[i];
            return null;
        }

        /// <summary>Sets an inventory slot's capacity while preserving its current stock.</summary>
        public bool SetCapacity(ResourceDefinition resource, float capacity)
        {
            if (!TryNormalizeMutation(resource, capacity, out float normalized))
                return false;

            InventoryEntry entry = GetOrCreate(resource);
            if (entry == null)
                return false;

            if (normalized + ResourceQuantityRules.WholeNumberEpsilon < entry.onHand)
                return false;

            entry.capacity = normalized;
            if (entry.reserved > entry.onHand)
                entry.reserved = entry.onHand;
            ReconcileOwnedReservations(resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
            return true;
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
            OnChanged?.Invoke(this, resource);
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
            ReconcileOwnedReservations(resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
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
            AddLegacyReservation(resource, normalized);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
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
            float legacyAmount = GetLegacyReservation(resource);
            float released = Mathf.Min(normalized, legacyAmount);
            entry.reserved = Mathf.Max(0f, entry.reserved - released);
            SetLegacyReservation(resource, legacyAmount - released);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
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
            float withdrawable = Mathf.Min(
                GetLegacyReservation(resource),
                Mathf.Min(entry.reserved, entry.onHand));
            float withdrawn = Mathf.Min(normalized, withdrawable);
            if (resource.IsDiscrete)
                withdrawn = Mathf.Floor(withdrawn + ResourceQuantityRules.WholeNumberEpsilon);
            entry.reserved -= withdrawn;
            SetLegacyReservation(resource, GetLegacyReservation(resource) - withdrawn);
            entry.onHand -= withdrawn;
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
            return withdrawn;
        }

        /// <summary>Creates a reservation owned by one physical freight allocation.</summary>
        public bool TryReserveOwned(
            ResourceDefinition resource,
            float amount,
            out InventoryReservationToken token)
        {
            token = null;
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return false;

            InventoryEntry entry = GetOrCreate(resource);
            if (entry == null)
                return false;
            entry.Refresh();
            if (entry.Available < normalized)
                return false;

            token = new InventoryReservationToken(this, resource, normalized);
            ownedReservations.Add(token);
            entry.reserved += normalized;
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
            return true;
        }

        /// <summary>Releases only the quantity owned by this reservation token.</summary>
        public float ReleaseOwned(InventoryReservationToken token)
        {
            if (!Owns(token) || !token.IsActive)
                return 0f;

            InventoryEntry entry = GetEntry(token.Resource);
            float released = Mathf.Min(
                token.Remaining,
                entry != null ? entry.reserved : 0f);
            if (entry == null)
            {
                token.Invalidate();
                return 0f;
            }

            entry.reserved = Mathf.Max(0f, entry.reserved - released);
            token.Reduce(released);
            if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                token.Invalidate();
            entry.Refresh();
            OnChanged?.Invoke(this, token.Resource);
            return released;
        }

        /// <summary>
        /// Moves reserved stock directly between inventories. Both inventories are
        /// updated before either change event fires, so observers see a conserved transfer.
        /// </summary>
        public float TransferOwnedTo(
            InventoryReservationToken token,
            InventoryComponent destination,
            float amount)
        {
            if (!Owns(token) || !token.IsActive || destination == null ||
                destination == this ||
                !TryNormalizeMutation(token.Resource, amount, out float normalized) ||
                normalized <= 0f)
            {
                return 0f;
            }

            InventoryEntry sourceEntry = GetEntry(token.Resource);
            if (sourceEntry == null)
                return 0f;
            InventoryEntry destinationEntry = destination.GetOrCreate(token.Resource);
            sourceEntry.Refresh();
            destinationEntry.Refresh();

            float transfer = Mathf.Min(
                normalized,
                Mathf.Min(token.Remaining,
                    Mathf.Min(sourceEntry.reserved,
                        Mathf.Min(sourceEntry.onHand,
                            destinationEntry.capacity - destinationEntry.onHand))));
            if (token.Resource.IsDiscrete)
                transfer = Mathf.Floor(transfer + ResourceQuantityRules.WholeNumberEpsilon);
            if (transfer <= 0f)
                return 0f;

            sourceEntry.onHand -= transfer;
            sourceEntry.reserved -= transfer;
            token.Reduce(transfer);
            if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                token.Invalidate();
            destinationEntry.onHand += transfer;
            sourceEntry.Refresh();
            destinationEntry.Refresh();

            OnChanged?.Invoke(this, token.Resource);
            destination.OnChanged?.Invoke(destination, token.Resource);
            return transfer;
        }

        /// <summary>Moves available, unreserved stock atomically into another inventory.</summary>
        public float TransferAvailableTo(
            InventoryComponent destination,
            ResourceDefinition resource,
            float amount)
        {
            if (destination == null || destination == this ||
                !TryNormalizeMutation(resource, amount, out float normalized) ||
                normalized <= 0f)
            {
                return 0f;
            }

            InventoryEntry sourceEntry = GetEntry(resource);
            if (sourceEntry == null)
                return 0f;
            InventoryEntry destinationEntry = destination.GetOrCreate(resource);
            sourceEntry.Refresh();
            destinationEntry.Refresh();
            float transfer = Mathf.Min(
                normalized,
                Mathf.Min(sourceEntry.Available,
                    destinationEntry.capacity - destinationEntry.onHand));
            if (resource.IsDiscrete)
                transfer = Mathf.Floor(transfer + ResourceQuantityRules.WholeNumberEpsilon);
            if (transfer <= 0f)
                return 0f;

            sourceEntry.onHand -= transfer;
            destinationEntry.onHand += transfer;
            sourceEntry.Refresh();
            destinationEntry.Refresh();
            OnChanged?.Invoke(this, resource);
            destination.OnChanged?.Invoke(destination, resource);
            return transfer;
        }

        private bool Owns(InventoryReservationToken token)
        {
            return token != null && token.Owner == this && ownedReservations.Contains(token);
        }

        private void AddLegacyReservation(ResourceDefinition resource, float amount)
        {
            SetLegacyReservation(resource, GetLegacyReservation(resource) + amount);
        }

        private float GetLegacyReservation(ResourceDefinition resource)
        {
            return resource != null && legacyReservations.TryGetValue(resource, out float amount)
                ? amount
                : 0f;
        }

        private void SetLegacyReservation(ResourceDefinition resource, float amount)
        {
            if (resource == null)
                return;
            if (amount <= ResourceQuantityRules.WholeNumberEpsilon)
                legacyReservations.Remove(resource);
            else
                legacyReservations[resource] = amount;
        }

        private void ReconcileOwnedReservations(ResourceDefinition resource, InventoryEntry entry)
        {
            float legacy = Mathf.Min(entry.reserved, GetLegacyReservation(resource));
            SetLegacyReservation(resource, legacy);
            float ownedLimit = Mathf.Max(0f, entry.reserved - legacy);
            float ownedTotal = 0f;
            for (int i = 0; i < ownedReservations.Count; i++)
            {
                InventoryReservationToken token = ownedReservations[i];
                if (token != null && token.IsActive && token.Resource == resource)
                    ownedTotal += token.Remaining;
            }

            float excess = Mathf.Max(0f, ownedTotal - ownedLimit);
            for (int i = ownedReservations.Count - 1; i >= 0 && excess > 0f; i--)
            {
                InventoryReservationToken token = ownedReservations[i];
                if (token == null || !token.IsActive || token.Resource != resource)
                    continue;
                float reduced = Mathf.Min(excess, token.Remaining);
                token.Reduce(reduced);
                excess -= reduced;
                if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                    token.Invalidate();
            }
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

    /// <summary>Identity and remaining quantity for one inventory-owned reservation.</summary>
    public sealed class InventoryReservationToken
    {
        internal InventoryReservationToken(
            InventoryComponent owner,
            ResourceDefinition resource,
            float amount)
        {
            Owner = owner;
            Resource = resource;
            Remaining = amount;
            IsActive = true;
        }

        internal InventoryComponent Owner { get; }
        public ResourceDefinition Resource { get; }
        public float Remaining { get; private set; }
        public bool IsActive { get; private set; }

        internal void Reduce(float amount)
        {
            Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, amount));
        }

        internal void Invalidate()
        {
            Remaining = 0f;
            IsActive = false;
        }
    }
}
