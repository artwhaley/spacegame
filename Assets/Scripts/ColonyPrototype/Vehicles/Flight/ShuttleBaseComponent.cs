using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Reusable personnel, freight, and docking fixture for a shuttle berth.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Base")]
    public sealed class ShuttleBaseComponent : MonoBehaviour
    {
        [SerializeField] private DockingPortComponent dockingPort;
        [SerializeField] private WorkplaceComponent pilotWorkplace;
        [SerializeField] private InventoryComponent depotInventory;
        [SerializeField] private LogisticsStockComponent depotStock;
        [SerializeField] private Transform dutyAnchor;

        public DockingPortComponent DockingPort => dockingPort;
        public WorkplaceComponent PilotWorkplace => pilotWorkplace;
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
            pilotWorkplace = workplace;
            depotInventory = inventory;
            depotStock = stock;
            dutyAnchor = anchor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (dockingPort == null) dockingPort = GetComponent<DockingPortComponent>();
            if (pilotWorkplace == null) pilotWorkplace = GetComponent<WorkplaceComponent>();
            if (depotInventory == null) depotInventory = GetComponent<InventoryComponent>();
            if (depotStock == null) depotStock = GetComponent<LogisticsStockComponent>();
        }
#endif
    }
}
