using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Physical movement modes understood by the personnel route model. B1 plans
    /// local walking plus the explicit B2 Shuttle transfer leg.
    /// </summary>
    public enum PersonnelRouteLegType
    {
        Walk,
        Shuttle
    }

    /// <summary>
    /// Supplies current pedestrian reachability and distance estimates. The
    /// gameplay route plan keeps this transient estimate rather than a NavMeshPath.
    /// </summary>
    public interface IPersonnelRouteProvider
    {
        bool TryEstimate(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRouteEstimate estimate);

        bool TryEstimateFrom(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            out PersonnelRouteEstimate estimate);
    }

    public readonly struct PersonnelRouteEstimate
    {
        public PersonnelRouteEstimate(
            Vector3 startPosition,
            Vector3 destinationPosition,
            float distance)
        {
            if (!IsFinite(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));

            StartPosition = startPosition;
            DestinationPosition = destinationPosition;
            Distance = distance;
        }

        public Vector3 StartPosition { get; }
        public Vector3 DestinationPosition { get; }
        public float Distance { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>One ordered physical leg in a person-only route.</summary>
    public sealed class PersonnelRouteLeg
    {
        public PersonnelRouteLeg(
            PersonnelRouteLegType type,
            Transform destination,
            float estimatedDistance)
            : this(type, destination, estimatedDistance, null, null)
        {
        }

        public PersonnelRouteLeg(
            PersonnelRouteLegType type,
            Transform destination,
            float estimatedDistance,
            ShuttleTransferEndpoint originEndpoint,
            ShuttleTransferEndpoint destinationEndpoint)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!float.IsFinite(estimatedDistance) || estimatedDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(estimatedDistance));

            Type = type;
            Destination = destination;
            EstimatedDistance = estimatedDistance;
            OriginEndpoint = originEndpoint;
            DestinationEndpoint = destinationEndpoint;
            if (type == PersonnelRouteLegType.Shuttle &&
                (originEndpoint == null || destinationEndpoint == null))
            {
                throw new ArgumentException("A Shuttle personnel leg requires both transfer endpoints.");
            }
        }

        public static PersonnelRouteLeg Shuttle(
            ShuttleTransferEndpoint origin,
            ShuttleTransferEndpoint destination,
            float estimatedDistance)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            return new PersonnelRouteLeg(
                PersonnelRouteLegType.Shuttle,
                destination.TransferAnchor,
                estimatedDistance,
                origin,
                destination);
        }

        public PersonnelRouteLegType Type { get; }
        public Transform Destination { get; }
        public float EstimatedDistance { get; }
        public ShuttleTransferEndpoint OriginEndpoint { get; }
        public ShuttleTransferEndpoint DestinationEndpoint { get; }
    }

    /// <summary>
    /// Immutable answer to "how does this person reach this destination?".
    /// It intentionally contains no Brain, activity, hunger, work, or need policy.
    /// </summary>
    public sealed class PersonnelRoutePlan
    {
        private readonly PersonnelRouteLeg[] legs;
        private readonly float totalEstimatedDistance;

        internal PersonnelRoutePlan(
            ColonistIdentity person,
            Transform finalDestination,
            PersonnelRouteLeg[] orderedLegs)
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));
            if (finalDestination == null)
                throw new ArgumentNullException(nameof(finalDestination));
            if (orderedLegs == null || orderedLegs.Length == 0)
                throw new ArgumentException("A route plan requires at least one leg.", nameof(orderedLegs));

            Person = person;
            FinalDestination = finalDestination;
            legs = (PersonnelRouteLeg[])orderedLegs.Clone();

            float distance = 0f;
            for (int index = 0; index < legs.Length; index++)
            {
                if (legs[index] == null || legs[index].Destination == null)
                    throw new ArgumentException("A route plan cannot contain a missing leg.", nameof(orderedLegs));
                distance += legs[index].EstimatedDistance;
            }

            totalEstimatedDistance = distance;
            StableId = BuildStableId();
        }

        public ColonistIdentity Person { get; }
        public Transform FinalDestination { get; }
        public IReadOnlyList<PersonnelRouteLeg> Legs => legs;
        public float TotalEstimatedDistance => totalEstimatedDistance;
        public string StableId { get; }

        private string BuildStableId()
        {
            StringBuilder stableId = new StringBuilder();
            stableId.Append(PersonnelRouteIdentity.GetStableKey(Person));
            stableId.Append("->");
            stableId.Append(PersonnelRouteIdentity.GetStableKey(FinalDestination));
            for (int index = 0; index < legs.Length; index++)
            {
                stableId.Append('|');
                stableId.Append(legs[index].Type);
                stableId.Append(':');
                stableId.Append(PersonnelRouteIdentity.GetStableKey(legs[index].Destination));
                if (legs[index].OriginEndpoint != null || legs[index].DestinationEndpoint != null)
                {
                    stableId.Append(':');
                    stableId.Append(legs[index].OriginEndpoint != null
                        ? legs[index].OriginEndpoint.StableId : string.Empty);
                    stableId.Append('>');
                    stableId.Append(legs[index].DestinationEndpoint != null
                        ? legs[index].DestinationEndpoint.StableId : string.Empty);
                }
            }
            return stableId.ToString();
        }
    }

    /// <summary>Stable scene/hierarchy keys used for repeatable route diagnostics and tie-breaks.</summary>
    public static class PersonnelRouteIdentity
    {
        public static string GetStableKey(UnityEngine.Object target)
        {
            return SceneStableIdentity.GetKey(target);
        }
    }
}
