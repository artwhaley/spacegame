namespace AsteroidColony
{
    /// <summary>Semantic physical facts reported by a freight leg executor to Logistics.</summary>
    public enum FreightExecutionReport
    {
        PickupRouteStarted,
        ProviderAtSource,
        LoadedRouteStarted,
        LoadedLegArrived,
        Failed
    }

    /// <summary>Typed freight failure cause; log text is derived only for diagnostics.</summary>
    public enum FreightFailureReason
    {
        None,
        PickupPreconditionFailed,
        AllocationNoLongerFits,
        WorkerInventoryMissing,
        ReservedStockUnavailable,
        DestinationOrCarrierUnavailable,
        OrderFulfilledCarrierRetainsExcessCargo,
        DestinationHasNoCapacity,
        CarrierRetainsUnacceptedCargo,
        PickupAnchorMissing,
        DropoffAnchorMissing,
        PersonnelRouteRunnerMissing,
        PickupRouteUnavailable,
        DropoffRouteUnavailable,
        FreightRouteFailed,
        WorkReleasedBeforePickup,
        StageReservationFailed,
        DemandPublicationExpired
    }
}
