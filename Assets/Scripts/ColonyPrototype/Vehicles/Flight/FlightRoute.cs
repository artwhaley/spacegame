using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum FlightWaypointKind
    {
        Cruise,
        Approach,
        Clearance
    }

    /// <summary>A world-space progression target consumed by Shuttle guidance.</summary>
    [Serializable]
    public sealed class FlightWaypoint
    {
        public Vector3 worldPosition;
        public bool hasDesiredOrientation;
        public Quaternion desiredOrientation = Quaternion.identity;
        public FlightWaypointKind kind = FlightWaypointKind.Cruise;
        [Min(0.01f)] public float arrivalRadius = 2f;
        public bool requiresLowArrivalSpeed;
        [Min(0f)] public float requiredArrivalSpeed = 0.5f;
        [Min(0f)] public float maxPassSpeed;

        public FlightWaypoint() { }

        public FlightWaypoint(Vector3 worldPosition, FlightWaypointKind kind, float arrivalRadius = 2f,
            bool requiresLowArrivalSpeed = false, float requiredArrivalSpeed = 0.5f,
            Quaternion? desiredOrientation = null)
        {
            this.worldPosition = worldPosition;
            this.kind = kind;
            this.arrivalRadius = Mathf.Max(0.01f, arrivalRadius);
            this.requiresLowArrivalSpeed = requiresLowArrivalSpeed;
            this.requiredArrivalSpeed = Mathf.Max(0f, requiredArrivalSpeed);
            hasDesiredOrientation = desiredOrientation.HasValue;
            this.desiredOrientation = desiredOrientation.HasValue
                ? ShuttleFlightIntegrator.Normalize(desiredOrientation.Value)
                : Quaternion.identity;
        }

        public bool IsValid => ShuttleFlightIntegrator.IsFinite(worldPosition) &&
            ShuttleFlightIntegrator.IsFinite(arrivalRadius) && arrivalRadius > 0f &&
            ShuttleFlightIntegrator.IsFinite(requiredArrivalSpeed) && requiredArrivalSpeed >= 0f &&
            ShuttleFlightIntegrator.IsFinite(maxPassSpeed) && maxPassSpeed >= 0f &&
            (!hasDesiredOrientation || ShuttleFlightIntegrator.IsFinite(desiredOrientation));

        public static FlightWaypoint Cruise(Vector3 position, float radius = 2f, float maxPassSpeed = 0f)
        {
            FlightWaypoint waypoint = new FlightWaypoint(position, FlightWaypointKind.Cruise, radius);
            waypoint.maxPassSpeed = Mathf.Max(0f, maxPassSpeed);
            return waypoint;
        }

        public static FlightWaypoint Approach(Vector3 position, Quaternion? orientation = null,
            float radius = 1f, float requiredSpeed = 1f)
        {
            return new FlightWaypoint(position, FlightWaypointKind.Approach, radius, true,
                requiredSpeed, orientation);
        }

        public static FlightWaypoint Clearance(Vector3 position, Quaternion? orientation = null,
            float radius = 1f, float requiredSpeed = 1f)
        {
            return new FlightWaypoint(position, FlightWaypointKind.Clearance, radius, true,
                requiredSpeed, orientation);
        }
    }

    /// <summary>
    /// Ordered targets from a route provider. The route owns no guidance,
    /// obstacle search, or transform movement.
    /// </summary>
    [Serializable]
    public sealed class FlightRoute
    {
        [SerializeField] private List<FlightWaypoint> waypoints = new List<FlightWaypoint>();

        public IReadOnlyList<FlightWaypoint> Waypoints => waypoints;
        public int Count => waypoints != null ? waypoints.Count : 0;
        public FlightWaypoint this[int index] => waypoints[index];

        public FlightRoute() { }

        public FlightRoute(IEnumerable<FlightWaypoint> initialWaypoints)
        {
            waypoints = new List<FlightWaypoint>();
            if (initialWaypoints == null)
                return;
            foreach (FlightWaypoint waypoint in initialWaypoints)
                if (waypoint != null)
                    waypoints.Add(waypoint);
        }

        public bool AddWaypoint(FlightWaypoint waypoint)
        {
            if (waypoint == null || !waypoint.IsValid)
                return false;
            if (waypoints == null)
                waypoints = new List<FlightWaypoint>();
            waypoints.Add(waypoint);
            return true;
        }

        public bool IsValid
        {
            get
            {
                if (waypoints == null || waypoints.Count == 0)
                    return false;
                for (int i = 0; i < waypoints.Count; i++)
                    if (waypoints[i] == null || !waypoints[i].IsValid)
                        return false;
                return true;
            }
        }

        /// <summary>Creates the direct Sprint A route through authored clearance and approach nodes.</summary>
        public static FlightRoute CreateDirect(Vector3 originClearance, Vector3 destinationApproach,
            Quaternion? approachOrientation = null, float clearanceRadius = 1f, float approachRadius = 1f)
        {
            return new FlightRoute(new[]
            {
                FlightWaypoint.Clearance(originClearance, null, clearanceRadius),
                FlightWaypoint.Approach(destinationApproach, approachOrientation, approachRadius)
            });
        }

        public static bool CanAdvancePastWaypoint(Vector3 position, Vector3 velocity, FlightWaypoint waypoint)
        {
            if (waypoint == null || !waypoint.IsValid ||
                !ShuttleFlightIntegrator.IsFinite(position) || !ShuttleFlightIntegrator.IsFinite(velocity))
                return false;
            if (Vector3.Distance(position, waypoint.worldPosition) > waypoint.arrivalRadius)
                return false;
            return !waypoint.requiresLowArrivalSpeed || velocity.magnitude <= waypoint.requiredArrivalSpeed;
        }
    }
}
