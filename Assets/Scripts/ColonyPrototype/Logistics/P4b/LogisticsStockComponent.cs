using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum LogisticsStockRole
    {
        Producer,
        Consumer,
        Depot
    }

    [Serializable]
    public sealed class LogisticsStockPolicyEntry
    {
        public ResourceDefinition resource;
        public LogisticsStockRole role;
        [Min(0f)] public float targetStock;
        public bool targetFull = true;
        [Min(0f)] public float reorderThreshold;
        [Min(0f)] public float emergencyThreshold;
        [Min(0.0001f)] public float minimumPickup = 1f;

        public float ResolveTarget(InventoryComponent inventory)
        {
            return targetFull && inventory != null
                ? inventory.GetCapacity(resource)
                : Mathf.Max(0f, targetStock);
        }
    }

    /// <summary>Local import/export policy for one resource in one physical inventory.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Stock Policy")]
    public sealed class LogisticsStockComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private static readonly List<LogisticsStockComponent> active =
            new List<LogisticsStockComponent>();

        [SerializeField] private InventoryComponent inventory;
        [SerializeField] private Transform freightAnchor;
        [SerializeField] private List<LogisticsStockPolicyEntry> policies =
            new List<LogisticsStockPolicyEntry>();

        public InventoryComponent Inventory => inventory;
        public Transform FreightAnchor => freightAnchor != null ? freightAnchor : transform;
        public IReadOnlyList<LogisticsStockPolicyEntry> Policies => policies;
        public static IReadOnlyList<LogisticsStockComponent> Active => active;
        public int SimulationTickPriority => SimulationTickPriorities.LogisticsStockPublication;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
        }

        private void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            SimulationManager.UnregisterTickable(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            active.Clear();
        }

        public bool TryGetPolicy(ResourceDefinition resource, out LogisticsStockPolicyEntry entry)
        {
            entry = null;
            if (resource == null || policies == null)
                return false;

            for (int i = 0; i < policies.Count; i++)
            {
                LogisticsStockPolicyEntry candidate = policies[i];
                if (candidate != null && candidate.resource == resource)
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }

        public LogisticsStockPolicyEntry ConfigurePolicy(
            ResourceDefinition resource,
            LogisticsStockRole role,
            float targetStock,
            bool targetFull,
            float reorderThreshold,
            float emergencyThreshold,
            float minimumPickup)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
            if (inventory == null)
                throw new InvalidOperationException(name + " needs an InventoryComponent.");

            if (policies == null)
                policies = new List<LogisticsStockPolicyEntry>();
            if (!TryGetPolicy(resource, out LogisticsStockPolicyEntry entry))
            {
                entry = new LogisticsStockPolicyEntry { resource = resource };
                policies.Add(entry);
            }

            entry.role = role;
            entry.targetStock = targetStock;
            entry.targetFull = targetFull;
            entry.reorderThreshold = reorderThreshold;
            entry.emergencyThreshold = emergencyThreshold;
            entry.minimumPickup = minimumPickup;
            ValidateEntry(entry);
            return entry;
        }

        public void SetBindings(InventoryComponent localInventory, Transform anchor)
        {
            inventory = localInventory;
            freightAnchor = anchor;
        }

        public string GetStableKey()
        {
            return SceneStableIdentity.GetKey(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            FreightLogisticsManager.Instance?.Publish(this);
        }

        private void OnValidate()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
            if (policies == null)
                return;
            for (int i = 0; i < policies.Count; i++)
                if (policies[i] != null)
                    ValidateEntry(policies[i]);
        }

        private void ValidateEntry(LogisticsStockPolicyEntry entry)
        {
            if (entry.resource == null)
                return;

            float target = entry.ResolveTarget(inventory);
            if (!ResourceQuantityRules.TryValidate(entry.resource, target, out string error) ||
                !ResourceQuantityRules.TryValidate(entry.resource, entry.reorderThreshold, out error) ||
                !ResourceQuantityRules.TryValidate(entry.resource, entry.emergencyThreshold, out error) ||
                !ResourceQuantityRules.TryValidate(entry.resource, entry.minimumPickup, out error) ||
                entry.minimumPickup <= 0f ||
                entry.emergencyThreshold > entry.reorderThreshold ||
                entry.reorderThreshold > target)
            {
                Debug.LogError(name + " has an invalid logistics policy for " +
                    entry.resource.name + ": " + (error ?? "threshold ordering is invalid"), this);
            }
        }
    }
}
