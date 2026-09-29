using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony
{
    /// <summary>
    /// The sole B1 provider of pedestrian route viability and distance. It returns
    /// transient estimates; ColonistMotor remains responsible for physical movement.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Navigation/Pedestrian Route Provider")]
    public sealed class PedestrianRouteProvider : MonoBehaviour, IPersonnelRouteProvider
    {
        private static readonly HashSet<string> s_Diagnostics =
            new HashSet<string>(StringComparer.Ordinal);

        private const int SharedPathCacheLimit = 512;
        private readonly Dictionary<string, SharedPathCacheEntry> sharedPathCache =
            new Dictionary<string, SharedPathCacheEntry>(StringComparer.Ordinal);
        private readonly Queue<string> sharedPathCacheOrder = new Queue<string>();
        private int sharedPathQueryCount;
        private int sharedPathCacheHitCount;

        [SerializeField, Min(0.01f)]
        private float destinationSampleDistance = 1f;

        [SerializeField]
        private int areaMask = NavMesh.AllAreas;

        [SerializeField]
        private int sharedAgentTypeId = 0;

        [SerializeField]
        private int sharedAreaMask = NavMesh.AllAreas;

        public int SharedPathQueryCount => sharedPathQueryCount;
        public int SharedPathCacheHitCount => sharedPathCacheHitCount;

        /// <summary>
        /// Checks a fixed freight/personnel anchor leg with the shared colonist
        /// navigation profile. This query has no worker or workforce dependency.
        /// </summary>
        public bool TryEstimateSharedPath(
            Transform start,
            Transform destination,
            out PersonnelRouteEstimate estimate,
            out string reason)
        {
            estimate = default;
            if (start == null || destination == null)
            {
                reason = "anchor_missing";
                return false;
            }

            bool hasStartRegion = ModuleNavigationTopology.TryGetRegion(start, out int startRegion);
            bool hasDestinationRegion = ModuleNavigationTopology.TryGetRegion(
                destination, out int destinationRegion);
            if (hasStartRegion && hasDestinationRegion && startRegion != destinationRegion)
            {
                reason = "different_walking_regions";
                return false;
            }

            string key = start.GetEntityId() + ":" + destination.GetEntityId() + ":" +
                sharedAgentTypeId + ":" + sharedAreaMask + ":" + ModuleNavigationTopology.Revision;
            if (sharedPathCache.TryGetValue(key, out SharedPathCacheEntry cached) &&
                cached.Start == start.position && cached.Destination == destination.position)
            {
                sharedPathCacheHitCount++;
                estimate = cached.Estimate;
                reason = cached.Reason;
                return cached.Available;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = sharedAgentTypeId,
                areaMask = sharedAreaMask
            };
            sharedPathQueryCount++;
            bool available = TryCalculatePath(start.position, destination.position,
                filter, out estimate, out reason);
            StoreSharedPath(key, new SharedPathCacheEntry(
                start.position, destination.position, available, estimate, reason));
            return available;
        }

        public bool TryEstimate(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            estimate = default;
            if (person == null)
                return false;

            return TryEstimateFrom(
                person,
                person.transform.position,
                destination,
                out estimate);
        }

        public bool TryEstimateFrom(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            estimate = default;
            if (person == null || destination == null ||
                !IsFinite(hypotheticalStart))
            {
                return false;
            }

            int effectiveAreaMask = ResolveAreaMask(person);
            NavMeshHit startHit;
            if (!NavMesh.SamplePosition(
                    hypotheticalStart,
                    out startHit,
                    destinationSampleDistance,
                    effectiveAreaMask))
            {
                LogDiagnostic(person, hypotheticalStart, destination, effectiveAreaMask,
                    "start_sample_failed", destinationSampleDistance);
                return false;
            }

            int personRegion = -1;
            bool hasPersonRegion = hypotheticalStart == person.transform.position &&
                ModuleNavigationTopology.TryGetRegion(person.transform, out personRegion);
            bool hasDestinationRegion = ModuleNavigationTopology.TryGetRegion(
                destination, out int destinationRegion);
            if (hasPersonRegion && hasDestinationRegion && personRegion != destinationRegion)
            {
                LogDiagnostic(person, hypotheticalStart, destination, ResolveAreaMask(person),
                    "different_walking_regions", destinationSampleDistance);
                return false;
            }

            NavMeshHit destinationHit;
            if (!NavMesh.SamplePosition(
                    destination.position,
                    out destinationHit,
                    destinationSampleDistance,
                    effectiveAreaMask))
            {
                LogDiagnostic(person, hypotheticalStart, destination, effectiveAreaMask,
                    "destination_sample_failed", destinationSampleDistance);
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(
                    startHit.position,
                    destinationHit.position,
                    effectiveAreaMask,
                    path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                path.corners == null)
            {
                LogDiagnostic(person, hypotheticalStart, destination, effectiveAreaMask,
                    "path_incomplete", destinationSampleDistance, path, startHit.position, destinationHit.position);
                return false;
            }

            float distance = 0f;
            for (int index = 1; index < path.corners.Length; index++)
            {
                distance += Vector3.Distance(path.corners[index - 1], path.corners[index]);
            }

            if (!IsFinite(distance))
                return false;

            estimate = new PersonnelRouteEstimate(
                startHit.position,
                destinationHit.position,
                distance);
            return true;
        }

        private int ResolveAreaMask(ColonistIdentity person)
        {
            NavMeshAgent agent = person.GetComponent<NavMeshAgent>();
            return agent != null ? agent.areaMask : areaMask;
        }

        private static bool TryCalculatePath(
            Vector3 startPosition,
            Vector3 destinationPosition,
            NavMeshQueryFilter filter,
            out PersonnelRouteEstimate estimate,
            out string reason)
        {
            estimate = default;
            if (!NavMesh.SamplePosition(startPosition, out NavMeshHit startHit, 1f, filter))
            {
                reason = "start_sample_failed";
                return false;
            }
            if (!NavMesh.SamplePosition(destinationPosition, out NavMeshHit destinationHit, 1f, filter))
            {
                reason = "destination_sample_failed";
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, destinationHit.position, filter, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners == null)
            {
                reason = "no_pedestrian_route";
                return false;
            }

            float distance = 0f;
            for (int index = 1; index < path.corners.Length; index++)
                distance += Vector3.Distance(path.corners[index - 1], path.corners[index]);
            if (!IsFinite(distance))
            {
                reason = "invalid_path_distance";
                return false;
            }

            estimate = new PersonnelRouteEstimate(startHit.position, destinationHit.position, distance);
            reason = string.Empty;
            return true;
        }

        private void StoreSharedPath(string key, SharedPathCacheEntry entry)
        {
            if (!sharedPathCache.ContainsKey(key))
                sharedPathCacheOrder.Enqueue(key);
            sharedPathCache[key] = entry;
            while (sharedPathCache.Count > SharedPathCacheLimit && sharedPathCacheOrder.Count > 0)
                sharedPathCache.Remove(sharedPathCacheOrder.Dequeue());
        }

        private readonly struct SharedPathCacheEntry
        {
            public SharedPathCacheEntry(Vector3 start, Vector3 destination,
                bool available, PersonnelRouteEstimate estimate, string reason)
            {
                Start = start;
                Destination = destination;
                Available = available;
                Estimate = estimate;
                Reason = reason;
            }

            public Vector3 Start { get; }
            public Vector3 Destination { get; }
            public bool Available { get; }
            public PersonnelRouteEstimate Estimate { get; }
            public string Reason { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDiagnostics()
        {
            s_Diagnostics.Clear();
        }

        private static void LogDiagnostic(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            int effectiveAreaMask,
            string stage,
            float sampleRadius,
            NavMeshPath path = null,
            Vector3? sampledStart = null,
            Vector3? sampledDestination = null)
        {
            string key = person.GetEntityId().ToString() + ":" +
                destination.GetEntityId().ToString() + ":" +
                stage;
            if (!s_Diagnostics.Add(key))
                return;

            string pathState = path == null
                ? "none"
                : path.status + "/corners=" + (path.corners == null ? 0 : path.corners.Length);
            string destinationPath = destination.name;
            for (Transform parent = destination.parent; parent != null; parent = parent.parent)
                destinationPath = parent.name + "/" + destinationPath;
            List<string> corners = new List<string>();
            if (path != null)
                foreach (Vector3 corner in path.corners)
                    corners.Add(corner.ToString("F4"));
            Debug.Log(
                "[B2Walk] pedestrian leg unavailable during route evaluation: person=" + person.name +
                ", destination=" + destinationPath +
                ", stage=" + stage +
                ", start=" + hypotheticalStart +
                ", destinationPosition=" + destination.position +
                ", sampleRadius=" + sampleRadius +
                ", areaMask=" + effectiveAreaMask +
                ", path=" + pathState +
                ", sampledStart=" + sampledStart + ", sampledDestination=" + sampledDestination +
                ", corners=[" + string.Join(" -> ", corners) + "].",
                destination);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
