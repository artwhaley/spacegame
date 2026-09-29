using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("minimumPickup")]
        [Min(0.0001f)] public float minimumShipmentQuantity = 1f;

        public float ResolveTarget(InventoryComponent inventory)
        {
            return targetFull && inventory != null
                ? inventory.Capacity
                : Mathf.Max(0f, targetStock);
        }

        internal LogisticsStockPolicyEntry CreateSnapshot()
        {
            return new LogisticsStockPolicyEntry
            {
                resource = resource,
                role = role,
                targetStock = targetStock,
                targetFull = targetFull,
                reorderThreshold = reorderThreshold,
                emergencyThreshold = emergencyThreshold,
                minimumShipmentQuantity = minimumShipmentQuantity
            };
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
        public IReadOnlyList<LogisticsStockPolicyEntry> Policies => CreatePolicySnapshots();
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
            FreightLogisticsManager.Instance?.StopPublishing(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            active.Clear();
        }

        public bool TryGetPolicy(ResourceDefinition resource, out LogisticsStockPolicyEntry entry)
        {
            entry = null;
            LogisticsStockPolicyEntry owned = FindPolicy(resource);
            if (owned == null)
                return false;
            entry = owned.CreateSnapshot();
            return true;
        }

        public LogisticsStockPolicyEntry ConfigurePolicy(
            ResourceDefinition resource,
            LogisticsStockRole role,
            float targetStock,
            bool targetFull,
            float reorderThreshold,
            float emergencyThreshold,
            float minimumShipmentQuantity)
        {
            if (!TryConfigurePolicy(resource, role, targetStock, targetFull,
                    reorderThreshold, emergencyThreshold, minimumShipmentQuantity,
                    out LogisticsStockPolicyEntry configured, out string reason))
                throw new ArgumentException(reason, nameof(resource));
            return configured;
        }

        public bool TryConfigurePolicy(
            ResourceDefinition resource,
            LogisticsStockRole role,
            float targetStock,
            bool targetFull,
            float reorderThreshold,
            float emergencyThreshold,
            float minimumShipmentQuantity,
            out LogisticsStockPolicyEntry configured,
            out string reason)
        {
            configured = null;
            reason = string.Empty;
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
            if (inventory == null)
            {
                reason = name + " needs an InventoryComponent.";
                return false;
            }

            List<LogisticsStockPolicyEntry> proposed = CreatePolicySnapshots();
            LogisticsStockPolicyEntry replacement = null;
            for (int index = 0; index < proposed.Count; index++)
                if (proposed[index].resource == resource)
                {
                    replacement = proposed[index];
                    break;
                }
            if (replacement == null)
            {
                replacement = new LogisticsStockPolicyEntry();
                proposed.Add(replacement);
            }
            replacement.resource = resource;
            replacement.role = role;
            replacement.targetStock = targetStock;
            replacement.targetFull = targetFull;
            replacement.reorderThreshold = reorderThreshold;
            replacement.emergencyThreshold = emergencyThreshold;
            replacement.minimumShipmentQuantity = minimumShipmentQuantity;

            if (!TryValidatePolicySet(proposed, inventory, out reason))
                return false;

            LogisticsStockPolicyEntry owned = FindPolicy(resource);
            if (owned == null)
            {
                owned = new LogisticsStockPolicyEntry();
                if (policies == null)
                    policies = new List<LogisticsStockPolicyEntry>();
                policies.Add(owned);
            }
            CopyPolicy(replacement, owned);
            configured = owned.CreateSnapshot();
            if (role != LogisticsStockRole.Consumer)
                FreightLogisticsManager.Instance?.StopPublishing(this, resource);
            else
                FreightLogisticsManager.Instance?.Publish(this);
            return true;
        }

        public void SetBindings(InventoryComponent localInventory, Transform anchor)
        {
            if (!TrySetBindings(localInventory, anchor, out string reason))
                throw new ArgumentException(reason, nameof(localInventory));
        }

        public bool TrySetBindings(
            InventoryComponent localInventory,
            Transform anchor,
            out string reason)
        {
            reason = string.Empty;
            if (localInventory == null)
            {
                reason = "a stock policy requires a physical inventory";
                return false;
            }
            bool bindingsChanged = inventory != localInventory || freightAnchor != anchor;
            if (bindingsChanged &&
                FreightLogisticsManager.Instance != null &&
                FreightLogisticsManager.Instance.HasLiveJobsForStock(this))
            {
                reason = "stock cannot be rebound while accepted freight obligations are live";
                return false;
            }
            if (!TryValidatePolicySet(policies, localInventory, out reason))
                return false;
            inventory = localInventory;
            freightAnchor = anchor;
            return true;
        }

        public bool TryRemovePolicy(ResourceDefinition resource, out string reason)
        {
            reason = string.Empty;
            int policyIndex = -1;
            if (resource != null && policies != null)
                for (int index = 0; index < policies.Count; index++)
                    if (policies[index] != null && policies[index].resource == resource)
                    {
                        policyIndex = index;
                        break;
                    }
            if (policyIndex < 0)
            {
                reason = "no policy exists for the requested resource";
                return false;
            }

            policies.RemoveAt(policyIndex);
            FreightLogisticsManager.Instance?.StopPublishing(this, resource);
            return true;
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
            if (policies == null || policies.Count == 0 || inventory == null)
                return;
            if (!TryValidatePolicySet(policies, inventory, out string reason))
                Debug.LogError(name + " has invalid logistics stock policies: " + reason, this);
        }

        private LogisticsStockPolicyEntry FindPolicy(ResourceDefinition resource)
        {
            if (resource == null || policies == null)
                return null;
            for (int index = 0; index < policies.Count; index++)
                if (policies[index] != null && policies[index].resource == resource)
                    return policies[index];
            return null;
        }

        private List<LogisticsStockPolicyEntry> CreatePolicySnapshots()
        {
            List<LogisticsStockPolicyEntry> snapshots = new List<LogisticsStockPolicyEntry>(
                policies != null ? policies.Count : 0);
            if (policies == null)
                return snapshots;
            for (int index = 0; index < policies.Count; index++)
                if (policies[index] != null)
                    snapshots.Add(policies[index].CreateSnapshot());
            return snapshots;
        }

        private static void CopyPolicy(
            LogisticsStockPolicyEntry source,
            LogisticsStockPolicyEntry destination)
        {
            destination.resource = source.resource;
            destination.role = source.role;
            destination.targetStock = source.targetStock;
            destination.targetFull = source.targetFull;
            destination.reorderThreshold = source.reorderThreshold;
            destination.emergencyThreshold = source.emergencyThreshold;
            destination.minimumShipmentQuantity = source.minimumShipmentQuantity;
        }

        private static bool TryValidatePolicySet(
            IReadOnlyList<LogisticsStockPolicyEntry> candidatePolicies,
            InventoryComponent stockInventory,
            out string reason)
        {
            reason = string.Empty;
            if (candidatePolicies == null || stockInventory == null)
            {
                reason = "inventory and policy collection are required";
                return false;
            }

            if (candidatePolicies.Count > 1)
            {
                for (int index = 0; index < candidatePolicies.Count; index++)
                    if (candidatePolicies[index] != null && candidatePolicies[index].targetFull)
                    {
                        reason = "targetFull requires a single-resource inventory policy";
                        return false;
                    }
            }

            float configuredTargetBudget = 0f;
            HashSet<ResourceDefinition> seenResources = new HashSet<ResourceDefinition>();
            for (int index = 0; index < candidatePolicies.Count; index++)
            {
                LogisticsStockPolicyEntry entry = candidatePolicies[index];
                if (entry == null || entry.resource == null)
                {
                    reason = "every policy requires a resource";
                    return false;
                }
                if (!seenResources.Add(entry.resource))
                {
                    reason = "duplicate policies for " + entry.resource.name;
                    return false;
                }
                if (!Enum.IsDefined(typeof(LogisticsStockRole), entry.role))
                {
                    reason = "invalid stock role for " + entry.resource.name;
                    return false;
                }

                float target = entry.ResolveTarget(stockInventory);
                string error = string.Empty;
                if ((!entry.targetFull &&
                     !ResourceQuantityRules.TryValidate(entry.resource, entry.targetStock, out error)) ||
                    !ResourceQuantityRules.TryValidate(entry.resource, target, out error) ||
                    !ResourceQuantityRules.TryValidate(entry.resource, entry.reorderThreshold, out error) ||
                    !ResourceQuantityRules.TryValidate(entry.resource, entry.emergencyThreshold, out error) ||
                    !ResourceQuantityRules.TryValidate(entry.resource, entry.minimumShipmentQuantity, out error) ||
                    !IsFiniteNonnegative(entry.targetStock) ||
                    !IsFiniteNonnegative(entry.reorderThreshold) ||
                    !IsFiniteNonnegative(entry.emergencyThreshold) ||
                    !IsFinitePositive(entry.minimumShipmentQuantity) ||
                    entry.emergencyThreshold > entry.reorderThreshold ||
                    entry.reorderThreshold > target)
                {
                    reason = "invalid quantity or threshold ordering for " + entry.resource.name +
                             (string.IsNullOrEmpty(error) ? string.Empty : ": " + error);
                    return false;
                }
                configuredTargetBudget += target;
            }

            if (configuredTargetBudget > stockInventory.Capacity + ResourceQuantityRules.WholeNumberEpsilon)
            {
                reason = "combined policy targets exceed shared inventory capacity";
                return false;
            }
            return true;
        }

        private static bool IsFiniteNonnegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
