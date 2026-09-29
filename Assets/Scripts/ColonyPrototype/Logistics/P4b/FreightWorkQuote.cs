using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Read-only proposal for one provider to carry a bounded freight leg.</summary>
    public interface IFreightLegQuote
    {
        IFreightLegProvider Provider { get; }
        LogisticsRouteLegType LegType { get; }
        float ShipmentQuantity { get; }
        float TripCapacity { get; }
        float TotalServiceCost { get; }
        string StableComparisonKey { get; }
    }

    /// <summary>Small quote/accept seam shared by walking and vehicle freight providers.</summary>
    public interface IFreightLegProvider
    {
        bool TryAcceptQuote(IFreightLegQuote quote, FreightDeliveryJob job,
            string executionId, out IFreightLegExecution execution);
    }

    public enum FreightWorkPurpose
    {
        RoutinePorter,
        ProducerOutboundAssist,
        ConsumerEmergencyPickup
    }

    /// <summary>A provider's proposal for executing one shipment's walking leg.</summary>
    public sealed class FreightWorkQuote : IFreightLegQuote
    {
        internal FreightWorkQuote(
            WalkingFreightWorkService provider,
            ColonistIdentity selectedWorker,
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            LogisticsStockComponent finalDestination,
            ResourceDefinition resource,
            float shipmentQuantity,
            float positioningCost,
            float loadedCargoTravelCost,
            float tripCapacity,
            FreightWorkPurpose purpose)
        {
            Provider = provider;
            SelectedWorker = selectedWorker;
            Source = source;
            Destination = destination;
            FinalDestination = finalDestination;
            Resource = resource;
            ShipmentQuantity = shipmentQuantity;
            PositioningCost = positioningCost;
            LoadedCargoTravelCost = loadedCargoTravelCost;
            TripCapacity = tripCapacity;
            Purpose = purpose;
            StableComparisonKey = provider.GetStableKey();
        }

        public WalkingFreightWorkService Provider { get; }
        IFreightLegProvider IFreightLegQuote.Provider => Provider;
        LogisticsRouteLegType IFreightLegQuote.LegType => LogisticsRouteLegType.WalkingCarrier;
        public float ShipmentQuantity { get; }
        public float TripCapacity { get; }
        public int EstimatedTripCount => TripCapacity > 0f
            ? Mathf.CeilToInt(ShipmentQuantity / TripCapacity)
            : int.MaxValue;
        public float PositioningCost { get; }
        public float LoadedCargoTravelCost { get; }
        public float TotalServiceCost => PositioningCost +
            LoadedCargoTravelCost * EstimatedTripCount;
        public FreightWorkPurpose Purpose { get; }
        public string StableComparisonKey { get; }

        internal ColonistIdentity SelectedWorker { get; }
        internal LogisticsStockComponent Source { get; }
        internal LogisticsStockComponent Destination { get; }
        internal LogisticsStockComponent FinalDestination { get; }
        internal ResourceDefinition Resource { get; }
        internal bool IsEmergency => Purpose == FreightWorkPurpose.ConsumerEmergencyPickup;
    }

    /// <summary>A no-side-effect proposal for one shuttle cargo leg.</summary>
    public sealed class ShuttleFreightQuote : IFreightLegQuote
    {
        internal ShuttleFreightQuote(ShuttleManager provider, LogisticsRouteLeg leg,
            float shipmentQuantity, float tripCapacity)
        {
            Provider = provider;
            Leg = leg;
            ShipmentQuantity = shipmentQuantity;
            TripCapacity = tripCapacity;
            TotalServiceCost = leg != null ? leg.EstimatedLoadedDistance : 0f;
            StableComparisonKey = provider != null && leg != null
                ? provider.GetStableKey() + "/" + leg.OriginEndpoint.StableId + "/" +
                  leg.DestinationEndpoint.StableId
                : string.Empty;
        }

        public ShuttleManager Provider { get; }
        IFreightLegProvider IFreightLegQuote.Provider => Provider;
        public LogisticsRouteLeg Leg { get; }
        public float ShipmentQuantity { get; }
        public float TripCapacity { get; }
        public float TotalServiceCost { get; }
        public string StableComparisonKey { get; }
        LogisticsRouteLegType IFreightLegQuote.LegType => LogisticsRouteLegType.ShuttleFreight;
    }
}
