using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// The narrow station-side handoff point shared by personnel, freight, and
    /// Shuttle scheduling. It describes a transfer location; it does not own
    /// transport policy or employment.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Transfer Endpoint")]
    public sealed class ShuttleTransferEndpoint : MonoBehaviour
    {
        private static readonly List<ShuttleTransferEndpoint> active =
            new List<ShuttleTransferEndpoint>();

        [SerializeField] private string stableId;
        [SerializeField] private DockingPortComponent dockingPort;
        [SerializeField] private Transform transferAnchor;
        [SerializeField, Min(0.25f)] private float transferArrivalRadius = 1.25f;
        [SerializeField] private InventoryComponent stagingInventory;
        [SerializeField] private LogisticsStockComponent depotStock;

        public static IReadOnlyList<ShuttleTransferEndpoint> Active => active;
        public string StableId => string.IsNullOrWhiteSpace(stableId)
            ? SceneStableIdentity.GetKey(this)
            : stableId;
        public DockingPortComponent DockingPort => dockingPort;
        public Transform TransferAnchor => transferAnchor != null ? transferAnchor : transform;
        public float TransferArrivalRadius => Mathf.Max(0.25f, transferArrivalRadius);
        public InventoryComponent StagingInventory => stagingInventory;
        public LogisticsStockComponent DepotStock => depotStock;

        private void Reset() => ResolveReferences();

        private void Awake() => ResolveReferences();

        private void OnEnable()
        {
            ResolveReferences();
            if (!active.Contains(this))
                active.Add(this);
            ShuttleManager.Instance?.RegisterEndpoint(this);
        }

        private void Start()
        {
            // Manager and endpoint execution order is intentionally not an
            // authoring contract. Register again after all Awake methods ran.
            ShuttleManager.Instance?.RegisterEndpoint(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            ShuttleManager.Instance?.UnregisterEndpoint(this);
        }

        public bool Validate(out string reason)
        {
            ResolveReferences();
            if (dockingPort == null)
            {
                reason = "docking_port_missing";
                return false;
            }
            if (!dockingPort.ValidateConfiguration(out reason))
                return false;
            if (TransferAnchor == null)
            {
                reason = "transfer_anchor_missing";
                return false;
            }
            if (stagingInventory == null)
            {
                reason = "staging_inventory_missing";
                return false;
            }
            if (depotStock == null)
            {
                reason = "depot_stock_missing";
                return false;
            }

            reason = "valid";
            return true;
        }

        public void Configure(string id, DockingPortComponent port,
            Transform anchor, InventoryComponent inventory,
            LogisticsStockComponent stock)
        {
            stableId = id ?? string.Empty;
            dockingPort = port;
            transferAnchor = anchor;
            stagingInventory = inventory;
            depotStock = stock;
        }

        private void ResolveReferences()
        {
            if (dockingPort == null)
                dockingPort = GetComponent<DockingPortComponent>();
            if (stagingInventory == null)
                stagingInventory = GetComponent<InventoryComponent>();
            if (depotStock == null)
                depotStock = GetComponent<LogisticsStockComponent>();
            if (transferAnchor == null)
            {
                Transform candidate = transform.Find("DutyAnchor");
                if (candidate == null)
                    candidate = transform.Find("WorkerAnchor");
                if (candidate != null)
                    transferAnchor = candidate;
            }
        }
    }
}
