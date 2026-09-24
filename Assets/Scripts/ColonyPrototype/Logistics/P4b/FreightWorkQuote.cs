using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>A provider's proposal for moving one cargo allocation by walking.</summary>
    public sealed class FreightWorkQuote
    {
        internal FreightWorkQuote(
            WalkingFreightWorkService provider,
            ColonistIdentity selectedWorker,
            LogisticsStockComponent source,
            LogisticsStockComponent destination,
            ResourceDefinition resource,
            float requestedQuantity,
            float maximumUsefulQuantity,
            float positioningCost,
            float loadedCargoTravelCost,
            float capacity,
            bool emergency)
        {
            Provider = provider;
            SelectedWorker = selectedWorker;
            Source = source;
            Destination = destination;
            Resource = resource;
            RequestedQuantity = requestedQuantity;
            MaximumUsefulQuantity = maximumUsefulQuantity;
            PositioningCost = positioningCost;
            LoadedCargoTravelCost = loadedCargoTravelCost;
            Capacity = capacity;
            IsEmergency = emergency;
            StableComparisonKey = provider.GetStableKey();
        }

        public WalkingFreightWorkService Provider { get; }
        public float MaximumUsefulQuantity { get; }
        public float PositioningCost { get; }
        public float LoadedCargoTravelCost { get; }
        public float TotalServiceCost => PositioningCost + LoadedCargoTravelCost;
        public string StableComparisonKey { get; }

        internal ColonistIdentity SelectedWorker { get; }
        internal LogisticsStockComponent Source { get; }
        internal LogisticsStockComponent Destination { get; }
        internal ResourceDefinition Resource { get; }
        internal float RequestedQuantity { get; }
        internal float Capacity { get; }
        internal bool IsEmergency { get; }
    }
}
