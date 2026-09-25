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

        [SerializeField, Min(0.01f)]
        private float destinationSampleDistance = 1f;

        [SerializeField]
        private int areaMask = NavMesh.AllAreas;

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
                hypotheticalStart.ToString() + ":" + stage;
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
            Debug.LogWarning(
                "[B2Walk] pedestrian route failed: person=" + person.name +
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
