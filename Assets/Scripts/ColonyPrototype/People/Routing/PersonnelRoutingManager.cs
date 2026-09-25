using System;
using System.Collections.Generic;
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
        public const string MissingRoutingManagerReason = "personnel_routing_manager_missing";
        public const string MissingProviderReason = "pedestrian_provider_missing";
        public const string NoRouteReason = "no_pedestrian_route";
        public const string NoShuttleRouteReason = "no_shuttle_route";

        [SerializeField]
        private PedestrianRouteProvider pedestrianProvider;

        [NonSerialized]
        private IPersonnelRouteProvider providerOverride;

        private static readonly HashSet<string> s_RouteDiagnostics =
            new HashSet<string>(StringComparer.Ordinal);

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
            s_RouteDiagnostics.Clear();
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
            string directReason;
            if (TryEstimate(person, destination, out PersonnelRouteEstimate estimate, out directReason))
            {
                plan = new PersonnelRoutePlan(person, destination, new[]
                {
                    new PersonnelRouteLeg(PersonnelRouteLegType.Walk, destination, estimate.Distance)
                });
                reason = string.Empty;
                return true;
            }

            ShuttleManager shuttleManager = ShuttleManager.Instance;
            if (shuttleManager == null)
            {
                LogRouteDiagnostic(person, destination, directReason, null,
                    0, 0, 0, 0, new List<string> { "shuttleManager=NULL" });
                reason = NoShuttleRouteReason;
                return false;
            }

            ShuttleTransferEndpoint[] endpoints = new ShuttleTransferEndpoint[shuttleManager.Endpoints.Count];
            for (int index = 0; index < shuttleManager.Endpoints.Count; index++)
                endpoints[index] = shuttleManager.Endpoints[index];
            Array.Sort(endpoints, CompareEndpoints);

            int originWalkFailures = 0;
            int serviceFailures = 0;
            int remoteWalkFailures = 0;
            int candidateCount = 0;
            List<string> candidateDiagnostics = new List<string>();

            for (int originIndex = 0; originIndex < endpoints.Length; originIndex++)
            {
                ShuttleTransferEndpoint origin = endpoints[originIndex];
                if (origin == null)
                    continue;

                string originReason;
                if (!TryEstimateWalkOnly(person, person.transform.position,
                    origin.TransferAnchor, out PersonnelRouteEstimate toOrigin, out originReason))
                {
                    originWalkFailures++;
                    candidateDiagnostics.Add(
                        "origin=" + origin.StableId +
                        " anchor=" + origin.TransferAnchor.position +
                        " toOrigin=FAIL(" + originReason + ")");
                    continue;
                }

                for (int destinationIndex = 0; destinationIndex < endpoints.Length; destinationIndex++)
                {
                    ShuttleTransferEndpoint remote = endpoints[destinationIndex];
                    if (remote == null || remote == origin)
                        continue;

                    candidateCount++;
                    bool canService = shuttleManager.CanService(
                        origin, remote, ShuttlePayloadType.Passenger);
                    if (!canService)
                    {
                        serviceFailures++;
                        candidateDiagnostics.Add(
                            "origin=" + origin.StableId +
                            " remote=" + remote.StableId +
                            " toOrigin=OK canService=FALSE");
                        continue;
                    }

                    string remoteReason;
                    if (!TryEstimateWalkOnly(person, remote.TransferAnchor.position,
                        destination, out PersonnelRouteEstimate fromRemote, out remoteReason))
                    {
                        remoteWalkFailures++;
                        candidateDiagnostics.Add(
                            "origin=" + origin.StableId +
                            " remote=" + remote.StableId +
                            " toOrigin=OK canService=TRUE fromRemote=FAIL(" + remoteReason + ")");
                        continue;
                    }

                    candidateDiagnostics.Add(
                        "origin=" + origin.StableId +
                        " remote=" + remote.StableId +
                        " toOrigin=OK canService=TRUE fromRemote=OK");

                    plan = new PersonnelRoutePlan(person, destination, new[]
                    {
                        new PersonnelRouteLeg(PersonnelRouteLegType.Walk,
                            origin.TransferAnchor, toOrigin.Distance),
                        PersonnelRouteLeg.Shuttle(origin, remote, 0f),
                        new PersonnelRouteLeg(PersonnelRouteLegType.Walk,
                            destination, fromRemote.Distance)
                    });
                    reason = string.Empty;
                    return true;
                }
            }

            LogRouteDiagnostic(person, destination, directReason, shuttleManager,
                endpoints.Length, candidateCount, originWalkFailures,
                serviceFailures + remoteWalkFailures, candidateDiagnostics);
            reason = NoShuttleRouteReason;
            return false;
        }

        private static void LogRouteDiagnostic(
            ColonistIdentity person,
            Transform destination,
            string directReason,
            ShuttleManager shuttleManager,
            int endpointCount,
            int candidateCount,
            int originWalkFailures,
            int downstreamFailures,
            List<string> candidateDiagnostics)
        {
            if (person == null || destination == null)
                return;

            string key = person.GetEntityId().ToString() + ":" +
                destination.GetEntityId().ToString();
            if (!s_RouteDiagnostics.Add(key))
                return;

            string shuttleManagerState = shuttleManager == null
                ? "NULL"
                : "endpoints=" + endpointCount + ", shuttles=" + shuttleManager.Shuttles.Count;
            string candidates = candidateDiagnostics == null || candidateDiagnostics.Count == 0
                ? "none"
                : string.Join(" | ", candidateDiagnostics.ToArray());

            Debug.LogWarning(
                "[B2Route] no_shuttle_route diagnostic: person=" + person.name +
                ", destination=" + destination.name +
                ", directWalk=FAIL(" + directReason + ")" +
                ", shuttleManager=" + shuttleManagerState +
                ", candidatePairs=" + candidateCount +
                ", originWalkFailures=" + originWalkFailures +
                ", downstreamFailures=" + downstreamFailures +
                ", candidates=[" + candidates + "]",
                destination);
        }

        public bool TryPlanWalkOnly(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRoutePlan plan,
            out string reason)
        {
            plan = null;
            if (!TryEstimateWalkOnly(person, person != null ? person.transform.position : Vector3.zero,
                destination, out PersonnelRouteEstimate estimate, out reason))
                return false;
            plan = new PersonnelRoutePlan(person, destination, new[]
            {
                new PersonnelRouteLeg(PersonnelRouteLegType.Walk, destination, estimate.Distance)
            });
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

        public bool TryEstimateWalkOnly(
            ColonistIdentity person,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            return TryEstimateWalkOnly(person,
                person != null ? person.transform.position : Vector3.zero,
                destination, out estimate, out _);
        }

        public bool TryEstimateWalkOnly(
            ColonistIdentity person,
            Vector3 hypotheticalStart,
            Transform destination,
            out PersonnelRouteEstimate estimate)
        {
            return TryEstimateWalkOnly(person, hypotheticalStart, destination,
                out estimate, out _);
        }

        public bool TryEstimateWalkOnly(
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
            if (!provider.TryEstimateFrom(person, hypotheticalStart, destination, out estimate))
            {
                reason = NoRouteReason;
                return false;
            }
            reason = string.Empty;
            return true;
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

        private static int CompareEndpoints(ShuttleTransferEndpoint left, ShuttleTransferEndpoint right)
        {
            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;
            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}
