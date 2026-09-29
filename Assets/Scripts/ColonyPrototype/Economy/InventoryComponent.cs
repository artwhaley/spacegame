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
        [NonSerialized] public float reserved;
        // Kept hidden so older scene/prefab data can migrate once to the shared
        // InventoryComponent capacity. This value is never used as a live bin limit.
        [HideInInspector] public float capacity;

        [NonSerialized] private float available;

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
                }

                onHand = Mathf.Max(0f, onHand);
                reserved = Mathf.Clamp(reserved, 0f, onHand);
                available = Mathf.Max(0f, onHand - reserved);
                return;
            }

            onHand = Mathf.Max(0f, onHand);
            reserved = Mathf.Clamp(reserved, 0f, onHand);
            available = Mathf.Max(0f, onHand - reserved);
        }

        internal float LegacyCapacity => capacity;
        internal void ClearLegacyCapacity() => capacity = 0f;

        internal InventoryEntry CreateSnapshot()
        {
            InventoryEntry snapshot = new InventoryEntry
            {
                resource = resource,
                onHand = onHand,
                reserved = reserved,
                capacity = capacity
            };
            snapshot.Refresh();
            return snapshot;
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
        [SerializeField, Min(0f)] private float capacity;
        [SerializeField, HideInInspector] private bool sharedCapacityInitialized;
        [NonSerialized] private List<InventoryReservationToken> ownedReservations =
            new List<InventoryReservationToken>();
        [NonSerialized] private Dictionary<ResourceDefinition, float> legacyReservations =
            new Dictionary<ResourceDefinition, float>();

        /// <summary>Read-only snapshots. Editing a snapshot never edits owned stock.</summary>
        public IReadOnlyList<InventoryEntry> Entries => CreateSnapshots();
        public static IReadOnlyList<InventoryComponent> Inventories => knownInventories;
        public event Action<InventoryComponent, ResourceDefinition> OnChanged;
        public float UsedCapacity => CalculateUsedCapacity();
        public float FreeCapacity
        {
            get => Mathf.Max(0f, capacity - UsedCapacity);
        }

        public float Capacity
        {
            get => capacity;
        }

        private void OnEnable()
        {
            MigrateSharedCapacityIfNeeded();
            EnsureCapacityCoversStock();
            EnsureReservationLedgers();
            ReconcileAllReservations();
            if (!knownInventories.Contains(this))
                knownInventories.Add(this);
        }

        private void OnDestroy()
        {
            knownInventories.Remove(this);
        }

        public InventoryEntry GetEntry(ResourceDefinition resource)
        {
            InventoryEntry entry = FindEntry(resource);
            return entry != null ? entry.CreateSnapshot() : null;
        }

        private InventoryEntry FindEntry(ResourceDefinition resource)
        {
            if (entries == null)
                return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].resource == resource)
                    return entries[i];
            return null;
        }

        private List<InventoryEntry> CreateSnapshots()
        {
            List<InventoryEntry> snapshots = new List<InventoryEntry>(entries != null ? entries.Count : 0);
            if (entries == null)
                return snapshots;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    snapshots.Add(entries[i].CreateSnapshot());
            return snapshots;
        }

        /// <summary>Sets the total shared capacity while preserving all current stock.</summary>
        public bool SetCapacity(float newCapacity)
        {
            if (float.IsNaN(newCapacity) || float.IsInfinity(newCapacity) || newCapacity < 0f)
                return false;

            MigrateSharedCapacityIfNeeded();
            if (newCapacity + ResourceQuantityRules.WholeNumberEpsilon < UsedCapacity)
                return false;

            capacity = newCapacity;
            sharedCapacityInitialized = true;
            return true;
        }

        /// <summary>Compatibility overload; capacity is inventory-wide, not resource-specific.</summary>
        public bool SetCapacity(ResourceDefinition resource, float capacity)
        {
            return resource != null && SetCapacity(capacity);
        }

        private InventoryEntry GetOrCreate(ResourceDefinition resource)
        {
            if (resource == null)
                return null;
            InventoryEntry entry = FindEntry(resource);
            if (entry == null)
            {
                entry = new InventoryEntry { resource = resource };
                entries.Add(entry);
            }
            return entry;
        }

        public float GetOnHand(ResourceDefinition resource)
        {
            InventoryEntry entry = FindEntry(resource);
            if (entry != null)
                ReconcileReservations(resource, entry);
            return entry != null ? entry.onHand : 0f;
        }

        public float GetAvailable(ResourceDefinition resource)
        {
            InventoryEntry entry = FindEntry(resource);
            if (entry == null)
                return 0f;
            ReconcileReservations(resource, entry);
            return entry.Available;
        }

        public float GetReserved(ResourceDefinition resource)
        {
            InventoryEntry entry = FindEntry(resource);
            if (entry != null)
                ReconcileReservations(resource, entry);
            return entry != null ? entry.reserved : 0f;
        }

        public float GetFreeCapacity(ResourceDefinition resource)
        {
            return resource != null ? FreeCapacity : 0f;
        }

        public float GetCapacity(ResourceDefinition resource)
        {
            return resource != null ? Capacity : 0f;
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
            float space = FreeCapacity;
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
            return FreeCapacity >= Mathf.Max(0f, normalized);
        }

        /// <summary>
        /// Atomically applies a recipe transformation. Inputs consume only unclaimed stock;
        /// all outputs fit against the net shared-capacity change before any quantity changes.
        /// </summary>
        public bool TryApplyRecipe(
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs,
            float scale,
            out string reason)
        {
            reason = string.Empty;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
            {
                reason = "recipe scale must be finite and nonnegative";
                return false;
            }

            Dictionary<ResourceDefinition, float> inputTotals = new Dictionary<ResourceDefinition, float>();
            Dictionary<ResourceDefinition, float> outputTotals = new Dictionary<ResourceDefinition, float>();
            if (!TryAccumulateRecipeAmounts(inputs, scale, inputTotals, out reason) ||
                !TryAccumulateRecipeAmounts(outputs, scale, outputTotals, out reason))
                return false;

            float consumedTotal = 0f;
            foreach (KeyValuePair<ResourceDefinition, float> input in inputTotals)
            {
                if (GetAvailable(input.Key) + ResourceQuantityRules.WholeNumberEpsilon < input.Value)
                {
                    reason = "recipe input unavailable for " + input.Key.name;
                    return false;
                }
                consumedTotal += input.Value;
            }

            float producedTotal = 0f;
            foreach (KeyValuePair<ResourceDefinition, float> output in outputTotals)
                producedTotal += output.Value;
            if (UsedCapacity - consumedTotal + producedTotal >
                Capacity + ResourceQuantityRules.WholeNumberEpsilon)
            {
                reason = "recipe outputs exceed shared inventory capacity after input consumption";
                return false;
            }

            Dictionary<ResourceDefinition, InventoryEntry> affectedEntries =
                new Dictionary<ResourceDefinition, InventoryEntry>();
            foreach (KeyValuePair<ResourceDefinition, float> input in inputTotals)
            {
                InventoryEntry entry = FindEntry(input.Key);
                if (entry == null)
                {
                    reason = "recipe input entry disappeared for " + input.Key.name;
                    return false;
                }
                affectedEntries[input.Key] = entry;
            }
            foreach (KeyValuePair<ResourceDefinition, float> output in outputTotals)
            {
                InventoryEntry entry = GetOrCreate(output.Key);
                if (entry == null)
                {
                    reason = "recipe output entry could not be created for " + output.Key.name;
                    return false;
                }
                affectedEntries[output.Key] = entry;
            }

            HashSet<ResourceDefinition> changed = new HashSet<ResourceDefinition>();
            foreach (KeyValuePair<ResourceDefinition, float> input in inputTotals)
            {
                InventoryEntry entry = affectedEntries[input.Key];
                entry.onHand -= input.Value;
                changed.Add(input.Key);
            }
            foreach (KeyValuePair<ResourceDefinition, float> output in outputTotals)
            {
                InventoryEntry entry = affectedEntries[output.Key];
                entry.onHand += output.Value;
                changed.Add(output.Key);
            }

            foreach (ResourceDefinition resource in changed)
            {
                InventoryEntry entry = affectedEntries[resource];
                ReconcileReservations(resource, entry);
                entry.Refresh();
            }
            foreach (ResourceDefinition resource in changed)
                OnChanged?.Invoke(this, resource);
            return true;
        }

        private static bool TryAccumulateRecipeAmounts(
            IReadOnlyList<ResourceAmount> amounts,
            float scale,
            Dictionary<ResourceDefinition, float> totals,
            out string reason)
        {
            reason = string.Empty;
            if (amounts == null)
                return true;

            for (int index = 0; index < amounts.Count; index++)
            {
                ResourceAmount entry = amounts[index];
                float amount = entry.amount * scale;
                if (entry.resource == null || float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                {
                    reason = "recipe contains an invalid resource amount";
                    return false;
                }
                if (amount <= ResourceQuantityRules.WholeNumberEpsilon)
                    continue;
                totals.TryGetValue(entry.resource, out float existing);
                totals[entry.resource] = existing + amount;
            }

            List<ResourceDefinition> resources = new List<ResourceDefinition>(totals.Keys);
            for (int index = 0; index < resources.Count; index++)
            {
                ResourceDefinition resource = resources[index];
                float amount = totals[resource];
                if (!ResourceQuantityRules.TryNormalize(resource, amount, out float normalized) ||
                    Mathf.Abs(normalized - amount) > ResourceQuantityRules.WholeNumberEpsilon)
                {
                    reason = "recipe quantity is invalid for " + resource.name;
                    return false;
                }
                totals[resource] = normalized;
            }
            return true;
        }

        /// <summary>
        /// Removes stock (consumption). Returns the amount actually removed.
        /// If removal dips below reserved stock, reservations are reduced to match.
        /// </summary>
        public float Remove(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return 0f;
            InventoryEntry entry = FindEntry(resource);
            if (entry == null || entry.onHand <= 0f)
                return 0f;
            ReconcileReservations(resource, entry);
            float removed = Mathf.Min(normalized, entry.onHand);
            if (resource.IsDiscrete)
                removed = Mathf.Floor(removed + ResourceQuantityRules.WholeNumberEpsilon);
            entry.onHand -= removed;
            ReconcileReservations(resource, entry);
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
            ReconcileReservations(resource, entry);
            if (entry.Available < normalized)
                return false;
            AddLegacyReservation(resource, normalized);
            ReconcileReservations(resource, entry);
            OnChanged?.Invoke(this, resource);
            return true;
        }

        /// <summary>Releases a previous reservation back to available stock.</summary>
        public void ReleaseReservation(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return;
            InventoryEntry entry = FindEntry(resource);
            if (entry == null)
                return;
            entry.Refresh();
            float legacyAmount = GetLegacyReservation(resource);
            float released = Mathf.Min(normalized, legacyAmount);
            SetLegacyReservation(resource, legacyAmount - released);
            ReconcileReservations(resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
        }

        /// <summary>Physically withdraws previously reserved stock. Returns the amount actually withdrawn.</summary>
        public float WithdrawReserved(ResourceDefinition resource, float amount)
        {
            if (!TryNormalizeMutation(resource, amount, out float normalized) || normalized <= 0f)
                return 0f;
            InventoryEntry entry = FindEntry(resource);
            if (entry == null)
                return 0f;
            ReconcileReservations(resource, entry);
            float withdrawable = Mathf.Min(
                GetLegacyReservation(resource),
                entry.onHand);
            float withdrawn = Mathf.Min(normalized, withdrawable);
            if (resource.IsDiscrete)
                withdrawn = Mathf.Floor(withdrawn + ResourceQuantityRules.WholeNumberEpsilon);
            SetLegacyReservation(resource, GetLegacyReservation(resource) - withdrawn);
            entry.onHand -= withdrawn;
            ReconcileReservations(resource, entry);
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
            ReconcileReservations(resource, entry);
            if (entry.Available < normalized)
                return false;

            token = new InventoryReservationToken(this, resource, normalized);
            EnsureReservationLedgers();
            ownedReservations.Add(token);
            ReconcileReservations(resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, resource);
            return true;
        }

        /// <summary>Releases only the quantity owned by this reservation token.</summary>
        public float ReleaseOwned(InventoryReservationToken token)
        {
            if (!Owns(token) || !token.IsActive)
                return 0f;

            InventoryEntry entry = FindEntry(token.Resource);
            if (entry == null)
            {
                token.Invalidate();
                return 0f;
            }

            float released = token.Remaining;
            token.Reduce(released);
            if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                token.Invalidate();
            ReconcileReservations(token.Resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, token.Resource);
            return released;
        }

        /// <summary>Consumes only stock still owned by this token, exactly once.</summary>
        public bool ConsumeOwned(InventoryReservationToken token, float amount)
        {
            if (!Owns(token) || !token.IsActive ||
                !TryNormalizeMutation(token.Resource, amount, out float normalized) ||
                normalized <= 0f)
                return false;

            InventoryEntry entry = FindEntry(token.Resource);
            if (entry == null)
                return false;
            ReconcileReservations(token.Resource, entry);
            if (!token.IsActive || token.Remaining + ResourceQuantityRules.WholeNumberEpsilon < normalized ||
                entry.onHand + ResourceQuantityRules.WholeNumberEpsilon < normalized)
                return false;

            token.Reduce(normalized);
            entry.onHand = Mathf.Max(0f, entry.onHand - normalized);
            if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                token.Invalidate();
            ReconcileReservations(token.Resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, token.Resource);
            return true;
        }

        /// <summary>
        /// Combines two reservations owned by this inventory into one logical claim.
        /// The incoming token is invalidated; physical stock is unchanged.
        /// </summary>
        public bool MergeOwnedReservations(
            InventoryReservationToken aggregate,
            InventoryReservationToken incoming)
        {
            if (aggregate == null || incoming == null || aggregate == incoming ||
                !Owns(aggregate) || !Owns(incoming) || !aggregate.IsActive ||
                !incoming.IsActive || aggregate.Resource != incoming.Resource)
            {
                return false;
            }

            InventoryEntry entry = FindEntry(aggregate.Resource);
            if (entry == null)
                return false;

            aggregate.Increase(incoming.Remaining);
            incoming.Invalidate();
            ownedReservations.Remove(incoming);
            ReconcileReservations(aggregate.Resource, entry);
            entry.Refresh();
            OnChanged?.Invoke(this, aggregate.Resource);
            return true;
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
            return TransferOwnedToInternal(token, destination, amount, false, out _);
        }

        /// <summary>
        /// Moves reserved stock and creates an owned reservation for the same quantity
        /// in the destination before either inventory publishes its change event.
        /// </summary>
        public float TransferOwnedToAndReserveDestination(
            InventoryReservationToken token,
            InventoryComponent destination,
            float amount,
            out InventoryReservationToken destinationReservation)
        {
            return TransferOwnedToInternal(token, destination, amount, true, out destinationReservation);
        }

        private float TransferOwnedToInternal(
            InventoryReservationToken token,
            InventoryComponent destination,
            float amount,
            bool reserveAtDestination,
            out InventoryReservationToken destinationReservation)
        {
            destinationReservation = null;
            if (!Owns(token) || !token.IsActive || destination == null ||
                destination == this ||
                !TryNormalizeMutation(token.Resource, amount, out float normalized) ||
                normalized <= 0f)
            {
                return 0f;
            }

            InventoryEntry sourceEntry = FindEntry(token.Resource);
            if (sourceEntry == null)
                return 0f;
            InventoryEntry destinationEntry = destination.GetOrCreate(token.Resource);
            if (destinationEntry == null)
                return 0f;
            ReconcileReservations(token.Resource, sourceEntry);
            destinationEntry.Refresh();

            float transfer = Mathf.Min(
                normalized,
                Mathf.Min(token.Remaining,
                    Mathf.Min(sourceEntry.onHand,
                        destination.FreeCapacity)));
            if (token.Resource.IsDiscrete)
                transfer = Mathf.Floor(transfer + ResourceQuantityRules.WholeNumberEpsilon);
            if (transfer <= 0f ||
                (reserveAtDestination &&
                 transfer + ResourceQuantityRules.WholeNumberEpsilon < normalized))
            {
                return 0f;
            }

            sourceEntry.onHand -= transfer;
            token.Reduce(transfer);
            if (token.Remaining <= ResourceQuantityRules.WholeNumberEpsilon)
                token.Invalidate();
            destinationEntry.onHand += transfer;
            if (reserveAtDestination)
            {
                destination.EnsureReservationLedgers();
                destinationReservation = new InventoryReservationToken(
                    destination, token.Resource, transfer);
                destination.ownedReservations.Add(destinationReservation);
                destination.ReconcileReservations(token.Resource, destinationEntry);
            }
            ReconcileReservations(token.Resource, sourceEntry);
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

            InventoryEntry sourceEntry = FindEntry(resource);
            if (sourceEntry == null)
                return 0f;
            InventoryEntry destinationEntry = destination.GetOrCreate(resource);
            sourceEntry.Refresh();
            destinationEntry.Refresh();
            float transfer = Mathf.Min(
                normalized,
                Mathf.Min(sourceEntry.Available,
                    destination.FreeCapacity));
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
            EnsureReservationLedgers();
            return token != null && token.Owner == this && ownedReservations.Contains(token);
        }

        private void EnsureReservationLedgers()
        {
            if (ownedReservations == null)
                ownedReservations = new List<InventoryReservationToken>();
            if (legacyReservations == null)
                legacyReservations = new Dictionary<ResourceDefinition, float>();
        }

        private void AddLegacyReservation(ResourceDefinition resource, float amount)
        {
            SetLegacyReservation(resource, GetLegacyReservation(resource) + amount);
        }

        private float GetLegacyReservation(ResourceDefinition resource)
        {
            EnsureReservationLedgers();
            return resource != null && legacyReservations.TryGetValue(resource, out float amount)
                ? amount
                : 0f;
        }

        private void SetLegacyReservation(ResourceDefinition resource, float amount)
        {
            EnsureReservationLedgers();
            if (resource == null)
                return;
            if (amount <= ResourceQuantityRules.WholeNumberEpsilon)
                legacyReservations.Remove(resource);
            else
                legacyReservations[resource] = amount;
        }

        private void ReconcileReservations(ResourceDefinition resource, InventoryEntry entry)
        {
            if (entry == null || resource == null)
                return;

            EnsureReservationLedgers();
            entry.Refresh();

            // The legacy API has one aggregate owner. Keep it first, then preserve
            // owned tokens in creation order and trim newer tokens when stock is short.
            float stockLimit = entry.onHand;
            float legacy = Mathf.Min(stockLimit, GetLegacyReservation(resource));
            SetLegacyReservation(resource, legacy);
            float ownedLimit = Mathf.Max(0f, stockLimit - legacy);
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

            for (int i = ownedReservations.Count - 1; i >= 0; i--)
                if (ownedReservations[i] == null || !ownedReservations[i].IsActive)
                    ownedReservations.RemoveAt(i);

            ownedTotal = 0f;
            for (int i = 0; i < ownedReservations.Count; i++)
            {
                InventoryReservationToken token = ownedReservations[i];
                if (token.Resource == resource)
                    ownedTotal += token.Remaining;
            }
            entry.reserved = legacy + ownedTotal;
            entry.Refresh();
        }

        private void ReconcileAllReservations()
        {
            EnsureReservationLedgers();
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry entry = entries[i];
                if (entry != null && entry.resource != null)
                    ReconcileReservations(entry.resource, entry);
                else if (entry != null)
                {
                    entry.reserved = 0f;
                    entry.Refresh();
                }
            }
        }

        private void OnValidate()
        {
            MigrateSharedCapacityIfNeeded();
            EnsureCapacityCoversStock();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                {
                    entries[i].reserved = 0f;
                    entries[i].Refresh();
                }
        }

        private void MigrateSharedCapacityIfNeeded()
        {
            if (sharedCapacityInitialized)
                return;

            float largestLegacyCapacity = 0f;
            float totalOnHand = 0f;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    InventoryEntry entry = entries[i];
                    if (entry == null)
                        continue;
                    entry.Refresh();
                    largestLegacyCapacity = Mathf.Max(largestLegacyCapacity, entry.LegacyCapacity);
                    totalOnHand += entry.onHand;
                }
            }

            if (capacity <= 0f && largestLegacyCapacity <= 0f && totalOnHand <= 0f)
                return;

            // Legacy capacity was per resource. Preserve the largest authored bin,
            // never sum those bins; total current stock still has to fit physically.
            capacity = Mathf.Max(capacity, Mathf.Max(largestLegacyCapacity, totalOnHand));
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] != null)
                        entries[i].ClearLegacyCapacity();
            sharedCapacityInitialized = true;
        }

        private float CalculateUsedCapacity()
        {
            float total = 0f;
            if (entries == null)
                return total;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    total += Mathf.Max(0f, entries[i].onHand);
            return total;
        }

        private void EnsureCapacityCoversStock()
        {
            float used = CalculateUsedCapacity();
            if (capacity + ResourceQuantityRules.WholeNumberEpsilon >= used)
                return;

            Debug.LogWarning($"{name}: shared inventory capacity {capacity:0.###} is below " +
                $"authored stock {used:0.###}; capacity was expanded during initialization to preserve stock.", this);
            capacity = used;
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

        internal void Increase(float amount)
        {
            Remaining += Mathf.Max(0f, amount);
        }

        internal void Invalidate()
        {
            Remaining = 0f;
            IsActive = false;
        }
    }
}
