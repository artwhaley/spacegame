namespace AsteroidColony
{
    /// <summary>Identity required when reporting facts about one accepted freight leg.</summary>
    public readonly struct FreightExecutionCorrelation
    {
        public FreightExecutionCorrelation(
            string allocationId,
            int legIndex,
            string executionId,
            string personnelRouteId = null,
            string personnelRouteStableId = null)
        {
            AllocationId = allocationId ?? string.Empty;
            LegIndex = legIndex;
            ExecutionId = executionId ?? string.Empty;
            PersonnelRouteId = personnelRouteId ?? string.Empty;
            PersonnelRouteStableId = personnelRouteStableId ?? string.Empty;
        }

        public string AllocationId { get; }
        public int LegIndex { get; }
        public string ExecutionId { get; }
        public string PersonnelRouteId { get; }
        public string PersonnelRouteStableId { get; }
        public bool HasPersonnelRoute =>
            !string.IsNullOrEmpty(PersonnelRouteId) &&
            !string.IsNullOrEmpty(PersonnelRouteStableId);
        public bool HasPartialPersonnelRoute =>
            string.IsNullOrEmpty(PersonnelRouteId) != string.IsNullOrEmpty(PersonnelRouteStableId);
    }

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
