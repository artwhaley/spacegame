using UnityEngine;
using UnityEngine.Serialization;

namespace AsteroidColony
{
    /// <summary>Reusable personnel, freight, and docking fixture for a shuttle berth.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Base")]
    public sealed class ShuttleBaseComponent : MonoBehaviour
    {
        [SerializeField] private DockingPortComponent dockingPort;
        [FormerlySerializedAs("pilotWorkplace")]
        [SerializeField] private WorkplaceComponent operationsWorkplace;
        [SerializeField] private InventoryComponent depotInventory;
        [SerializeField] private LogisticsStockComponent depotStock;
        [SerializeField] private Transform dutyAnchor;

        public DockingPortComponent DockingPort => dockingPort;
        public WorkplaceComponent OperationsWorkplace => operationsWorkplace;
        public WorkplaceComponent Workplace => operationsWorkplace;
        // Compatibility alias for existing editor tooling and prefab data. B2
        // treats this as one multi-role operations workplace, not a Pilot-only
        // authority.
        public WorkplaceComponent PilotWorkplace => operationsWorkplace;
        public InventoryComponent DepotInventory => depotInventory;
        public LogisticsStockComponent DepotStock => depotStock;
        public Transform DutyAnchor => dutyAnchor;

        public void Configure(
            DockingPortComponent port,
            WorkplaceComponent workplace,
            InventoryComponent inventory,
            LogisticsStockComponent stock,
            Transform anchor)
        {
            dockingPort = port;
            operationsWorkplace = workplace;
            depotInventory = inventory;
            depotStock = stock;
            dutyAnchor = anchor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (dockingPort == null) dockingPort = GetComponent<DockingPortComponent>();
            if (operationsWorkplace == null) operationsWorkplace = GetComponent<WorkplaceComponent>();
            if (depotInventory == null) depotInventory = GetComponent<InventoryComponent>();
            if (depotStock == null) depotStock = GetComponent<LogisticsStockComponent>();
        }
#endif
    }
}
