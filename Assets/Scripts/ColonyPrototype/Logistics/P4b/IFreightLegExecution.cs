using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Runtime execution responsible for physically completing one route leg.</summary>
    public interface IFreightLegExecution
    {
        string ExecutionId { get; }
        int LegIndex { get; }
        bool IsActive { get; }
        bool ProviderAvailable { get; }
        bool ExecutorAvailable { get; }
        bool IsEmergency { get; }
        bool RequiresPersonnelRouteForPickup { get; }
        bool RequiresPersonnelRouteForLoadedArrival { get; }
        bool CompletesAcceptedQuantityAcrossLoads { get; }
        bool DefersReleaseUntilAcceptedWorkCompletes { get; }
        bool HasPendingWorkerRelease { get; }
        float TripCapacity { get; }
        float PositioningDistance { get; }
        InventoryComponent CargoInventory { get; }
        UnityEngine.Object ProviderContext { get; }

        void Complete(FreightDeliveryJob job);
    }
}
