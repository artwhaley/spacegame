using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// The facts a food request carries. FoodManager does not own these facts; it evaluates
    /// registered services against them so that "what food service is available to *this*
    /// colonist from this position?" can be answered instead of only "which one is nearest?".
    /// </summary>
    public sealed class FoodQuery
    {
        public FoodQuery(
            ColonistIdentity seeker,
            Vector3 seekerPosition,
            float currentGameHour)
        {
            Seeker = seeker;
            SeekerPosition = seekerPosition;
            CurrentGameHour = currentGameHour;
        }

        public ColonistIdentity Seeker { get; }
        public Vector3 SeekerPosition { get; }
        public float CurrentGameHour { get; }
    }

    public sealed class FoodServiceOpportunity
    {
        public FoodServiceOpportunity(
            ActivityTarget target,
            FoodServiceComponent service,
            FoodServiceAccessMode accessMode)
        {
            Target = target;
            Service = service;
            AccessMode = accessMode;
        }

        public ActivityTarget Target { get; }
        public FoodServiceComponent Service { get; }
        public FoodServiceAccessMode AccessMode { get; }
    }

    [DisallowMultipleComponent]
    public sealed class FoodManager : MonoBehaviour
    {
        public static FoodManager Instance { get; private set; }

        [SerializeField]
        private List<FoodServiceComponent> services = new List<FoodServiceComponent>();

        private string lastAnonymousSignature;
        private readonly List<SeekerSignature> seekerSignatures =
            new List<SeekerSignature>();

        public IReadOnlyList<FoodServiceComponent> Services =>
            services ?? (IReadOnlyList<FoodServiceComponent>)Array.Empty<FoodServiceComponent>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active FoodManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            RegisterExistingServices();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(FoodServiceComponent service)
        {
            if (service == null)
                return;

            PruneServices();
            if (!services.Contains(service))
                services.Add(service);
        }

        public void Unregister(FoodServiceComponent service)
        {
            if (service == null || services == null)
                return;

            services.Remove(service);
        }

        public bool TryFindFoodTarget(
            FoodQuery query,
            out ActivityTarget target)
        {
            if (TryFindFoodService(query, out FoodServiceOpportunity opportunity))
            {
                target = opportunity.Target;
                RecordSelection(query, opportunity);
                return target != null && target.IsConfigured;
            }

            target = null;
            RecordSelection(query, null);
            return false;
        }

        public bool TryFindFoodService(
            FoodQuery query,
            out FoodServiceOpportunity opportunity)
        {
            opportunity = null;
            if (query == null)
                return false;

            PruneServices();

            float bestDistance = float.PositiveInfinity;
            FoodServiceComponent bestService = null;
            FoodServiceAccessMode bestAccessMode = FoodServiceAccessMode.Unavailable;
            for (int index = 0; index < services.Count; index++)
            {
                FoodServiceComponent candidate = services[index];
                if (!TryEvaluateCandidate(
                        query,
                        candidate,
                        out FoodServiceAccessMode accessMode,
                        out FacilityActivityBinding binding))
                {
                    continue;
                }

                float distance =
                    (binding.ApproachAnchor.position - query.SeekerPosition).sqrMagnitude;
                if (bestService == null ||
                    distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     IsPreferredTieBreak(candidate, bestService)))
                {
                    bestDistance = distance;
                    bestService = candidate;
                    bestAccessMode = accessMode;
                }
            }

            if (bestService == null)
                return false;

            opportunity = new FoodServiceOpportunity(
                new ActivityTarget(bestService.Facility, bestService.EatActivityId),
                bestService,
                bestAccessMode);
            return true;
        }

        public bool TryFindFoodService(
            FoodQuery query,
            out FoodServiceComponent service)
        {
            service = null;
            if (!TryFindFoodService(query, out FoodServiceOpportunity opportunity))
                return false;

            service = opportunity.Service;
            return service != null;
        }

        public bool TryFindFoodTarget(
            Vector3 seekerPosition,
            out ActivityTarget target)
        {
            return TryFindFoodTarget(new FoodQuery(null, seekerPosition, CurrentGameHour), out target);
        }

        public bool TryFindFoodService(
            Vector3 seekerPosition,
            out FoodServiceComponent service)
        {
            return TryFindFoodService(
                new FoodQuery(null, seekerPosition, CurrentGameHour),
                out service);
        }

        /// <summary>
        /// Structural lookup only: the service still exists and is authored correctly. This is
        /// what an already-eating colonist is checked against, because current staffing access
        /// must not interrupt a meal that has already been served.
        /// </summary>
        public bool TryGetFoodService(
            ActivityTarget target,
            out FoodServiceComponent service)
        {
            service = null;
            if (target == null || !target.IsConfigured)
                return false;

            PruneServices();
            for (int index = 0; index < services.Count; index++)
            {
                FoodServiceComponent candidate = services[index];
                if (candidate != null &&
                    candidate.Facility == target.Facility &&
                    string.Equals(
                        candidate.EatActivityId,
                        target.ActivityId,
                        StringComparison.Ordinal) &&
                    IsConfiguredService(candidate))
                {
                    service = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsFoodTargetLive(ActivityTarget target)
        {
            return TryGetFoodService(target, out _);
        }

        public bool TryGetFoodOpportunity(
            ActivityTarget target,
            ColonistIdentity requester,
            float currentGameHour,
            out FoodServiceOpportunity opportunity)
        {
            opportunity = null;
            if (!TryGetFoodService(target, out FoodServiceComponent service))
                return false;

            opportunity = new FoodServiceOpportunity(
                target,
                service,
                service.EvaluateAccess(requester, currentGameHour));
            return true;
        }

        /// <summary>
        /// Eligibility to *obtain* food, re-checked while an Eat request is still being
        /// pursued. Losing self-service permission or the public worker before the meal
        /// actually starts stops the request; once the meal is genuinely active this is not
        /// consulted again.
        /// </summary>
        public bool CanRequesterStillAccess(
            ActivityTarget target,
            ColonistIdentity requester,
            float currentGameHour)
        {
            return TryGetFoodService(target, out FoodServiceComponent service) &&
                   service.EvaluateAccess(requester, currentGameHour) !=
                   FoodServiceAccessMode.Unavailable;
        }

        public static string DescribeAccessMode(FoodServiceAccessMode accessMode)
        {
            switch (accessMode)
            {
                case FoodServiceAccessMode.Public:
                    return "public";
                case FoodServiceAccessMode.PublicStaffed:
                    return "public_staffed";
                case FoodServiceAccessMode.SelfService:
                    return "self_service";
                default:
                    return "unavailable";
            }
        }

        private bool TryEvaluateCandidate(
            FoodQuery query,
            FoodServiceComponent service,
            out FoodServiceAccessMode accessMode,
            out FacilityActivityBinding binding)
        {
            accessMode = FoodServiceAccessMode.Unavailable;
            binding = null;
            if (!IsConfiguredService(service))
                return false;

            if (!service.TryGetEatBinding(out binding))
                return false;

            if (service.Facility.IsReserved(binding.ReservationGroup))
                return false;

            accessMode = service.EvaluateAccess(query.Seeker, query.CurrentGameHour);
            return accessMode != FoodServiceAccessMode.Unavailable;
        }

        private bool IsConfiguredService(FoodServiceComponent service)
        {
            return service != null && service.IsConfigured;
        }

        private void RegisterExistingServices()
        {
            FoodServiceComponent[] existing = FindObjectsByType<FoodServiceComponent>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < existing.Length; index++)
                Register(existing[index]);
        }

        private static bool IsPreferredTieBreak(
            FoodServiceComponent candidate,
            FoodServiceComponent current)
        {
            if (current == null)
                return true;

            int nameComparison = string.CompareOrdinal(candidate.name, current.name);
            return nameComparison < 0;
        }

        private void RecordSelection(
            FoodQuery query,
            FoodServiceOpportunity opportunity)
        {
            ColonistIdentity seeker = query != null ? query.Seeker : null;
            string serviceName = opportunity != null && opportunity.Service != null
                ? opportunity.Service.name
                : "none";
            string accessMode = opportunity != null
                ? DescribeAccessMode(opportunity.AccessMode)
                : "unavailable";

            // Deduplication is per seeker. With more than one colonist asking for food a
            // single global signature would silently swallow one colonist's decisions.
            string signature = SeekerKey(seeker) + "|" + serviceName + "|" + accessMode;
            if (IsDuplicateSignature(seeker, signature))
                return;

            if (opportunity == null)
            {
                SimulationLogManager.RecordEvent(
                    "food.no_target",
                    "Food",
                    "Info",
                    seeker,
                    null,
                    new SimulationLogField("reason", "no_accessible_food_service"));
                return;
            }

            SimulationLogManager.RecordEvent(
                "food.target_selected",
                "Food",
                "Info",
                seeker,
                opportunity.Service != null ? opportunity.Service.Facility : null,
                new SimulationLogField("activityId", opportunity.Service != null
                    ? opportunity.Service.EatActivityId
                    : string.Empty),
                new SimulationLogField("accessMode", accessMode),
                new SimulationLogField("reason", "nearest_accessible_service"));
        }

        private bool IsDuplicateSignature(ColonistIdentity seeker, string signature)
        {
            if (seeker == null)
            {
                if (string.Equals(lastAnonymousSignature, signature, StringComparison.Ordinal))
                    return true;

                lastAnonymousSignature = signature;
                return false;
            }

            for (int index = seekerSignatures.Count - 1; index >= 0; index--)
            {
                SeekerSignature entry = seekerSignatures[index];
                if (entry.Seeker == null)
                {
                    seekerSignatures.RemoveAt(index);
                    continue;
                }

                if (!ReferenceEquals(entry.Seeker, seeker))
                    continue;

                if (string.Equals(entry.Signature, signature, StringComparison.Ordinal))
                    return true;

                seekerSignatures[index] = new SeekerSignature(seeker, signature);
                return false;
            }

            seekerSignatures.Add(new SeekerSignature(seeker, signature));
            return false;
        }

        private static string SeekerKey(ColonistIdentity seeker)
        {
            return seeker != null ? seeker.name : "anonymous";
        }

        private static float CurrentGameHour =>
            SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentGameHour
                : 0f;

        private void PruneServices()
        {
            if (services == null)
                services = new List<FoodServiceComponent>();

            for (int index = services.Count - 1; index >= 0; index--)
            {
                FoodServiceComponent service = services[index];
                if (service == null)
                    services.RemoveAt(index);
            }
        }

        private readonly struct SeekerSignature
        {
            public SeekerSignature(ColonistIdentity seeker, string signature)
            {
                Seeker = seeker;
                Signature = signature;
            }

            public ColonistIdentity Seeker { get; }
            public string Signature { get; }
        }
    }
}
