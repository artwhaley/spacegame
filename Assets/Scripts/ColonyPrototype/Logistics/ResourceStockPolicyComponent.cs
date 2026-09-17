using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [System.Serializable]
    public class ResourceStockPolicyEntry
    {
        public ResourceDefinition resource;

        [Header("Foreground import")]
        public bool normalDemandEnabled;
        [Tooltip("Foreground demand activates at or below this local stock.")]
        public float reorderThreshold;
        [Tooltip("Desired local stock, including any inbound freight.")]
        public float targetStock;
        [Range(1, 10)] public int priority = 5;
        [Tooltip("Planner will not dispatch less than this amount.")]
        public float minimumShipment;
        [Tooltip("Planner will cap each delivery at this amount; zero means uncapped.")]
        public float maximumShipment;

        [Header("Background buffer fill")]
        public bool backgroundFillEnabled;
        public float backgroundTargetStock;
        [Range(1, 10)] public int backgroundPriority = 1;

        [Header("Export")]
        public bool exportEnabled;
        public float retainStock;

        [System.NonSerialized] public int foregroundDemandId;
        [System.NonSerialized] public int backgroundDemandId;
        [System.NonSerialized] public int exportSupplyId;
    }

    /// <summary>
    /// Publishes reusable import/export policy for one local inventory. The policy
    /// owns no production and does not choose a source; LogisticsManager does that.
    /// </summary>
    public class ResourceStockPolicyComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public LocationAnchor location;
        public InventoryComponent inventory;
        public List<ResourceStockPolicyEntry> entries = new List<ResourceStockPolicyEntry>();

        private const float QuantityEpsilon = 0.0001f;
        private LogisticsManager registeredManager;

        public IReadOnlyList<ResourceStockPolicyEntry> Entries => entries;
        public int SimulationTickPriority => 300;

        private void Awake()
        {
            if (location == null)
                location = GetComponent<LocationAnchor>();
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
        }

        private void OnEnable()
        {
            RegisterPolicies();
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            UnregisterPolicies();
            SimulationManager.UnregisterTickable(this);
        }

        /// <summary>Refreshes registrations and snapshots; useful for tests and authoring tools.</summary>
        public void RefreshPolicy()
        {
            RegisterPolicies();
            PublishSnapshots();
        }

        public void SimulationTick(float deltaGameHours)
        {
            RegisterPolicies();
            PublishSnapshots();
        }

        private void RegisterPolicies()
        {
            if (LogisticsManager.Instance == null || location == null || inventory == null)
                return;

            LogisticsManager logistics = LogisticsManager.Instance;
            if (registeredManager != logistics)
            {
                // Demand IDs belong to one manager instance. A replacement manager
                // starts with fresh registries, so force policy re-registration.
                for (int i = 0; i < entries.Count; i++)
                {
                    ResourceStockPolicyEntry entry = entries[i];
                    if (entry == null)
                        continue;
                    entry.foregroundDemandId = 0;
                    entry.backgroundDemandId = 0;
                    entry.exportSupplyId = 0;
                }
                registeredManager = logistics;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ResourceStockPolicyEntry entry = entries[i];
                if (entry == null || !ValidateEntry(entry))
                    continue;

                if (entry.normalDemandEnabled && entry.foregroundDemandId <= 0)
                    entry.foregroundDemandId = LogisticsManager.Instance.RegisterFreightDemand(
                        $"{name} {entry.resource.displayName} foreground",
                        entry.resource, location, inventory, Mathf.Clamp(entry.priority, 1, 10),
                        entry.minimumShipment, entry.maximumShipment, FreightDemandClass.Foreground);
                if (entry.foregroundDemandId > 0)
                    LogisticsManager.Instance.UpdateFreightDemandPolicy(
                        entry.foregroundDemandId, entry.priority, entry.minimumShipment,
                        entry.maximumShipment, FreightDemandClass.Foreground);
                if (!entry.normalDemandEnabled && entry.foregroundDemandId > 0)
                {
                    LogisticsManager.Instance.UnregisterFreightDemand(entry.foregroundDemandId);
                    entry.foregroundDemandId = 0;
                }
                if (entry.backgroundFillEnabled && entry.backgroundDemandId <= 0)
                    entry.backgroundDemandId = LogisticsManager.Instance.RegisterFreightDemand(
                        $"{name} {entry.resource.displayName} background",
                        entry.resource, location, inventory, Mathf.Clamp(entry.backgroundPriority, 1, 10),
                        entry.minimumShipment, entry.maximumShipment, FreightDemandClass.Background);
                if (entry.backgroundDemandId > 0)
                    LogisticsManager.Instance.UpdateFreightDemandPolicy(
                        entry.backgroundDemandId, entry.backgroundPriority, entry.minimumShipment,
                        entry.maximumShipment, FreightDemandClass.Background);
                if (!entry.backgroundFillEnabled && entry.backgroundDemandId > 0)
                {
                    LogisticsManager.Instance.UnregisterFreightDemand(entry.backgroundDemandId);
                    entry.backgroundDemandId = 0;
                }
                if (entry.exportEnabled && entry.exportSupplyId <= 0)
                    entry.exportSupplyId = LogisticsManager.Instance.RegisterFreightSupply(
                        $"{name} {entry.resource.displayName} export",
                        entry.resource, location, inventory, entry.retainStock);
                if (entry.exportEnabled && entry.exportSupplyId > 0)
                    LogisticsManager.Instance.RegisterFreightSupply(
                        $"{name} {entry.resource.displayName} export",
                        entry.resource, location, inventory, entry.retainStock);
                if (!entry.exportEnabled && entry.exportSupplyId > 0)
                {
                    LogisticsManager.Instance.UnregisterFreightSupply(entry.exportSupplyId);
                    entry.exportSupplyId = 0;
                }
            }
        }

        private void PublishSnapshots()
        {
            if (LogisticsManager.Instance == null || inventory == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                ResourceStockPolicyEntry entry = entries[i];
                if (entry == null || entry.resource == null)
                    continue;

                float onHand = inventory.GetOnHand(entry.resource);
                float foregroundInbound = ActiveInbound(entry.foregroundDemandId);
                float backgroundInbound = ActiveInbound(entry.backgroundDemandId);
                float effectiveStock = onHand + foregroundInbound + backgroundInbound;
                bool foregroundActive = false;

                if (entry.foregroundDemandId > 0)
                {
                    float desired = 0f;
                    foregroundActive = entry.normalDemandEnabled &&
                        effectiveStock <= entry.reorderThreshold + QuantityEpsilon;
                    if (foregroundActive)
                        desired = Mathf.Max(0f, entry.targetStock - effectiveStock);
                    LogisticsManager.Instance.UpdateFreightDemand(entry.foregroundDemandId, desired);
                }

                if (entry.backgroundDemandId > 0)
                {
                    // Background fill only covers stock not already claimed by a
                    // foreground request, so the two identities cannot double-count.
                    float desired = !foregroundActive && entry.backgroundFillEnabled
                        ? Mathf.Max(0f, entry.backgroundTargetStock - effectiveStock)
                        : 0f;
                    LogisticsManager.Instance.UpdateFreightDemand(entry.backgroundDemandId, desired);
                }
            }
        }

        private float ActiveInbound(int demandId)
        {
            return demandId > 0 && ContractManager.Instance != null
                ? ContractManager.Instance.GetActiveFreightQuantityForDemand(demandId)
                : 0f;
        }

        private void UnregisterPolicies()
        {
            if (LogisticsManager.Instance == null)
                return;
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceStockPolicyEntry entry = entries[i];
                if (entry == null)
                    continue;
                LogisticsManager.Instance.UnregisterFreightDemand(entry.foregroundDemandId);
                LogisticsManager.Instance.UnregisterFreightDemand(entry.backgroundDemandId);
                LogisticsManager.Instance.UnregisterFreightSupply(entry.exportSupplyId);
                entry.foregroundDemandId = 0;
                entry.backgroundDemandId = 0;
                entry.exportSupplyId = 0;
            }
            registeredManager = null;
        }

        private static bool ValidateEntry(ResourceStockPolicyEntry entry)
        {
            if (entry.resource == null)
                return false;
            return ValidateAmount(entry.resource, entry.reorderThreshold, "reorder threshold") &&
                ValidateAmount(entry.resource, entry.targetStock, "target stock") &&
                ValidateAmount(entry.resource, entry.minimumShipment, "minimum shipment") &&
                ValidateAmount(entry.resource, entry.maximumShipment, "maximum shipment") &&
                ValidateAmount(entry.resource, entry.backgroundTargetStock, "background target stock") &&
                ValidateAmount(entry.resource, entry.retainStock, "retain stock");
        }

        private static bool ValidateAmount(ResourceDefinition resource, float amount, string label)
        {
            if (ResourceQuantityRules.TryValidate(resource, amount, out string error))
                return true;
            Debug.LogError($"Invalid {label} for {resource.name}: {error}");
            return false;
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceStockPolicyEntry entry = entries[i];
                if (entry == null || entry.resource == null)
                    continue;
                entry.priority = Mathf.Clamp(entry.priority, 1, 10);
                entry.backgroundPriority = Mathf.Clamp(entry.backgroundPriority, 1, 10);
            }
        }
    }
}
