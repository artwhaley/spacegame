using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Runtime execution responsible for physically completing one route leg.</summary>
    public interface IFreightLegExecution
    {
        int LegIndex { get; }
        bool IsActive { get; }
        bool IsEmergency { get; }
        float PositioningDistance { get; }
        InventoryComponent CargoInventory { get; }
        UnityEngine.Object ProviderContext { get; }

        void Complete(FreightDeliveryJob job);
    }
}
