using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Loaded cargo movement kinds. B2 adds a physical Shuttle leg.</summary>
    public enum LogisticsRouteLegType
    {
        WalkingCarrier,
        ShuttleFreight
    }

    /// <summary>Pure lexicographic ranking for two already route-valid allocations.</summary>
    public static class FreightCandidateRanking
    {
        private const float QuantityEpsilon = 0.0001f;

        public static int Compare(float leftQuantity, float leftDistance, string leftStableKey,
            float rightQuantity, float rightDistance, string rightStableKey)
        {
            if (Mathf.Abs(leftQuantity - rightQuantity) > QuantityEpsilon)
                return rightQuantity.CompareTo(leftQuantity);
            int distance = leftDistance.CompareTo(rightDistance);
            return distance != 0 ? distance :
                string.CompareOrdinal(leftStableKey ?? string.Empty, rightStableKey ?? string.Empty);
        }
    }

    /// <summary>A loaded movement segment in a freight route, between stock transfer points.</summary>
    public sealed class LogisticsRouteLeg
    {
        public LogisticsRouteLeg(LogisticsRouteLegType type, LogisticsStockComponent origin,
            LogisticsStockComponent destination, float estimatedLoadedDistance)
            : this(type, origin, destination, estimatedLoadedDistance, null, null)
        {
        }

        public LogisticsRouteLeg(LogisticsRouteLegType type, LogisticsStockComponent origin,
            LogisticsStockComponent destination, float estimatedLoadedDistance,
            ShuttleTransferEndpoint originEndpoint,
            ShuttleTransferEndpoint destinationEndpoint)
        {
            if (origin == null)
                throw new ArgumentNullException(nameof(origin));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (float.IsNaN(estimatedLoadedDistance) ||
                float.IsInfinity(estimatedLoadedDistance) || estimatedLoadedDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(estimatedLoadedDistance));
            }

            Type = type;
            Origin = origin;
            Destination = destination;
            EstimatedLoadedDistance = estimatedLoadedDistance;
            OriginEndpoint = originEndpoint;
            DestinationEndpoint = destinationEndpoint;
        }

        public static LogisticsRouteLeg Shuttle(
            LogisticsStockComponent origin,
            LogisticsStockComponent destination,
            ShuttleTransferEndpoint originEndpoint,
            ShuttleTransferEndpoint destinationEndpoint,
            float estimatedLoadedDistance)
        {
            return new LogisticsRouteLeg(LogisticsRouteLegType.ShuttleFreight,
                origin, destination, estimatedLoadedDistance, originEndpoint, destinationEndpoint);
        }

        public LogisticsRouteLegType Type { get; }
        public LogisticsStockComponent Origin { get; }
        public LogisticsStockComponent Destination { get; }
        public float EstimatedLoadedDistance { get; }
        public ShuttleTransferEndpoint OriginEndpoint { get; }
        public ShuttleTransferEndpoint DestinationEndpoint { get; }
    }

    /// <summary>Ordered cargo movement chosen for one freight allocation.</summary>
    public sealed class LogisticsRoutePlan
    {
        private readonly LogisticsRouteLeg[] legs;

        public LogisticsRoutePlan(FreightOrder order, IEnumerable<LogisticsRouteLeg> orderedLegs)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            if (orderedLegs == null)
                throw new ArgumentNullException(nameof(orderedLegs));

            Order = order;
            legs = new List<LogisticsRouteLeg>(orderedLegs).ToArray();
            if (legs.Length == 0)
                throw new ArgumentException("A freight route requires at least one leg.", nameof(orderedLegs));

            float distance = 0f;
            for (int index = 0; index < legs.Length; index++)
            {
                if (legs[index] == null)
                    throw new ArgumentException("A freight route cannot contain a missing leg.", nameof(orderedLegs));
                if (index > 0 && legs[index - 1].Destination != legs[index].Origin)
                    throw new ArgumentException(
                        "Each freight leg must begin where the previous leg ends.", nameof(orderedLegs));
                distance += legs[index].EstimatedLoadedDistance;
            }
            TotalEstimatedLoadedDistance = distance;
        }

        public FreightOrder Order { get; }
        public IReadOnlyList<LogisticsRouteLeg> Legs => legs;
        public float TotalEstimatedLoadedDistance { get; }
    }

    /// <summary>Demand/source/quantity/cargo route. It does not own a worker or execution.</summary>
    public sealed class FreightAllocation
    {
        internal FreightAllocation(string id, FreightOrder order, LogisticsStockComponent source,
            float quantity, LogisticsRoutePlan routePlan)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An allocation requires a stable ID.", nameof(id));
            if (order == null || source == null || routePlan == null)
                throw new ArgumentNullException("An allocation requires its order, source, and route plan.");
            if (float.IsNaN(quantity) || float.IsInfinity(quantity) || quantity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (routePlan.Order != order || routePlan.Legs[0].Origin != source ||
                routePlan.Legs[routePlan.Legs.Count - 1].Destination != order.Requester)
            {
                throw new ArgumentException(
                    "The route plan must carry this order from the selected source to its requester.",
                    nameof(routePlan));
            }

            Id = id;
            Order = order;
            Source = source;
            Quantity = quantity;
            RoutePlan = routePlan;
        }

        public string Id { get; }
        public FreightOrder Order { get; }
        public ResourceDefinition Resource => Order.Resource;
        public LogisticsStockComponent Source { get; }
        public LogisticsStockComponent FinalDestination => Order.Requester;
        public LogisticsStockComponent Destination => FinalDestination;
        public float Quantity { get; }
        public LogisticsRoutePlan RoutePlan { get; }
    }
}
