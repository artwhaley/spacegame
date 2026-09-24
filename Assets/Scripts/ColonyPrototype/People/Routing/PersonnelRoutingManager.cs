using System;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Selects how a person reaches a destination that policy has already chosen.
    /// B1 has one mode: a complete local pedestrian route.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Navigation/Personnel Routing Manager")]
    public sealed class PersonnelRoutingManager : MonoBehaviour
    {
        public const string MissingPersonReason = "person_missing";
        public const string MissingDestinationReason = "destination_missing";
        public const string MissingProviderReason = "pedestrian_provider_missing";
        public const string NoRouteReason = "no_pedestrian_route";

        [SerializeField]
        private PedestrianRouteProvider pedestrianProvider;

        [NonSerialized]
        private IPersonnelRouteProvider providerOverride;

        public static PersonnelRoutingManager Instance { get; private set; }

        public IPersonnelRouteProvider RouteProvider =>
            providerOverride != null ? providerOverride : pedestrianProvider;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active PersonnelRoutingManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (pedestrianProvider == null)
                pedestrianProvider = GetComponent<PedestrianRouteProvider>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Supplies a provider explicitly for scene composition or deterministic tests.
        /// Production authoring normally assigns PedestrianRouteProvider on this component.
        /// </summary>
        public void ConfigureProvider(IPersonnelRouteProvider provider)
        {
            providerOverride = provider;
            pedestrianProvider = provider as PedestrianRouteProvider;
        }

        public bool TryPlanRoute(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRoutePlan plan,
            out string reason)
        {
            plan = null;
            if (!TryEstimate(person, destination, out PersonnelRouteEstimate estimate, out reason))
                return false;

            PersonnelRouteLeg[] legs =
            {
                new PersonnelRouteLeg(
                    PersonnelRouteLegType.Walk,
                    destination,
                    estimate.Distance)
            };
            plan = new PersonnelRoutePlan(person, destination, legs);
            return true;
        }

        public bool TryEstimate(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            return TryEstimate(person, destination, out estimate, out _);
        }

        public bool TryEstimate(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRouteEstimate estimate,
            out string reason)
        {
            estimate = default;
            if (person == null)
            {
                reason = MissingPersonReason;
                return false;
            }

            return TryEstimateFrom(
                person,
                person.transform.position,
                destination,
                out estimate,
                out reason);
        }

        public bool TryEstimateFrom(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            return TryEstimateFrom(
                person,
                hypotheticalStart,
                destination,
                out estimate,
                out _);
        }

        public bool TryEstimateFrom(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            out PersonnelRouteEstimate estimate,
            out string reason)
        {
            estimate = default;
            if (person == null)
            {
                reason = MissingPersonReason;
                return false;
            }
            if (destination == null)
            {
                reason = MissingDestinationReason;
                return false;
            }

            IPersonnelRouteProvider provider = ResolveProvider();
            if (provider == null)
            {
                reason = MissingProviderReason;
                return false;
            }

            if (!provider.TryEstimateFrom(
                    person,
                    hypotheticalStart,
                    destination,
                    out estimate))
            {
                reason = NoRouteReason;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private IPersonnelRouteProvider ResolveProvider()
        {
            IPersonnelRouteProvider provider = RouteProvider;
            if (provider is UnityEngine.Object unityObject && unityObject == null)
                return null;
            return provider;
        }
    }
}
