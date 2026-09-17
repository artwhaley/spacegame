using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Rebuildable presentation seam for physical racks/boxes. It reads the
    /// InventoryComponent and never stores or mutates authoritative quantities.
    /// </summary>
    public class InventoryRackView : MonoBehaviour
    {
        public InventoryComponent inventory;
        [SerializeField] private List<string> visibleCargo = new List<string>();

        public IReadOnlyList<string> VisibleCargo => visibleCargo;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponentInParent<InventoryComponent>();
        }

        private void OnEnable()
        {
            if (inventory != null)
                inventory.OnChanged += HandleInventoryChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.OnChanged -= HandleInventoryChanged;
        }

        public void Refresh()
        {
            if (visibleCargo == null)
                visibleCargo = new List<string>();
            visibleCargo.Clear();
            if (inventory == null)
                return;
            IReadOnlyList<InventoryEntry> entries = inventory.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry entry = entries[i];
                if (entry == null || entry.resource == null || entry.onHand <= 0f)
                    continue;
                visibleCargo.Add($"{entry.resource.displayName}: {entry.onHand:0.##}");
            }
        }

        private void HandleInventoryChanged(InventoryComponent changed, ResourceDefinition resource)
        {
            Refresh();
        }
    }
}
