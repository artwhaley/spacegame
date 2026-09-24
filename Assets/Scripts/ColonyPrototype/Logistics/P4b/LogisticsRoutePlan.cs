using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Physical carrier modes that Logistics can assign. B1 implements local walking only.</summary>
    public enum LogisticsRouteLegType
    {
        WalkingCarrier
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

    /// <summary>One estimated physical leg in a freight allocation's route.</summary>
    public sealed class LogisticsRouteLeg
    {
        public LogisticsRouteLeg(LogisticsRouteLegType type, Vector3 startPosition,
            Transform destination, float estimatedDistance)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (float.IsNaN(estimatedDistance) || float.IsInfinity(estimatedDistance) || estimatedDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(estimatedDistance));

            Type = type;
            StartPosition = startPosition;
            Destination = destination;
            EstimatedDistance = estimatedDistance;
        }

        public LogisticsRouteLegType Type { get; }
        public Vector3 StartPosition { get; }
        public Transform Destination { get; }
        public float EstimatedDistance { get; }
    }

    /// <summary>Logistics-owned route choice for one accepted freight allocation.</summary>
    public sealed class LogisticsRoutePlan
    {
        private readonly LogisticsRouteLeg[] legs;

        public LogisticsRoutePlan(FreightOrder order, WalkingFreightCarrierComponent carrier,
            IEnumerable<LogisticsRouteLeg> orderedLegs)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            if (carrier == null)
                throw new ArgumentNullException(nameof(carrier));
            if (orderedLegs == null)
                throw new ArgumentNullException(nameof(orderedLegs));

            Order = order;
            Carrier = carrier;
            legs = new List<LogisticsRouteLeg>(orderedLegs).ToArray();
            if (legs.Length == 0)
                throw new ArgumentException("A freight route requires at least one leg.", nameof(orderedLegs));
            for (int index = 0; index < legs.Length; index++)
                if (legs[index] == null)
                    throw new ArgumentException("A freight route cannot contain a missing leg.", nameof(orderedLegs));

            float distance = 0f;
            for (int index = 0; index < legs.Length; index++)
                distance += legs[index].EstimatedDistance;
            TotalEstimatedDistance = distance;
        }

        public FreightOrder Order { get; }
        public WalkingFreightCarrierComponent Carrier { get; }
        public IReadOnlyList<LogisticsRouteLeg> Legs => legs;
        public float TotalEstimatedDistance { get; }
    }

    /// <summary>Accepted source, quantity, carrier, and route owned by FreightLogisticsManager.</summary>
    public sealed class FreightAllocation
    {
        internal FreightAllocation(string id, FreightOrder order, LogisticsStockComponent source,
            WalkingFreightCarrierComponent carrier, float quantity, LogisticsRoutePlan routePlan)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An allocation requires a stable ID.", nameof(id));
            if (order == null || source == null || carrier == null || routePlan == null)
                throw new ArgumentNullException("An allocation requires its order, source, carrier, and route plan.");
            if (float.IsNaN(quantity) || float.IsInfinity(quantity) || quantity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            Id = id;
            Order = order;
            Source = source;
            Carrier = carrier;
            Quantity = quantity;
            RoutePlan = routePlan;
        }

        public string Id { get; }
        public FreightOrder Order { get; }
        public LogisticsStockComponent Source { get; }
        public LogisticsStockComponent Destination => Order.Requester;
        public ResourceDefinition Resource => Order.Resource;
        public WalkingFreightCarrierComponent Carrier { get; }
        public float Quantity { get; internal set; }
        public LogisticsRoutePlan RoutePlan { get; }
    }
}
