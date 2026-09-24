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
            if (!NavMesh.SamplePosition(
                    hypotheticalStart,
                    out NavMeshHit startHit,
                    destinationSampleDistance,
                    effectiveAreaMask) ||
                !NavMesh.SamplePosition(
                    destination.position,
                    out NavMeshHit destinationHit,
                    destinationSampleDistance,
                    effectiveAreaMask))
            {
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

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
