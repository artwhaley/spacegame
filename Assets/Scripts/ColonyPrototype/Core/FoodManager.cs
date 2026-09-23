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
    public sealed class FoodManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public static FoodManager Instance { get; private set; }

        [SerializeField]
        private List<FoodServiceComponent> services = new List<FoodServiceComponent>();

        [NonSerialized] private readonly List<FoodBid> bidsThisRound = new List<FoodBid>();
        [NonSerialized] private readonly Dictionary<ColonistIdentity, FoodOffer> offers =
            new Dictionary<ColonistIdentity, FoodOffer>();

        private long foodQueryCount;
        private long foodCandidateEvaluationCount;
        private long foodSelectionCount;

        public IReadOnlyList<FoodServiceComponent> Services =>
            services ?? (IReadOnlyList<FoodServiceComponent>)Array.Empty<FoodServiceComponent>();
        public long FoodQueryCount => foodQueryCount;
        public long FoodCandidateEvaluationCount => foodCandidateEvaluationCount;
        public long FoodSelectionCount => foodSelectionCount;
        public int SimulationTickPriority => 150;
        public IReadOnlyList<FoodBid> BidsThisRound => bidsThisRound;

        public IReadOnlyCollection<FoodOffer> Offers => offers.Values;

        public void ResetDiagnostics()
        {
            foodQueryCount = 0;
            foodCandidateEvaluationCount = 0;
            foodSelectionCount = 0;
        }

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

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            bidsThisRound.Clear();
            offers.Clear();
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

        /// <summary>Submits a Food request for the current brain round.</summary>
        public void SubmitBid(FoodBid bid)
        {
            if (bid == null || bid.Requester == null)
                return;

            for (int index = bidsThisRound.Count - 1; index >= 0; index--)
            {
                FoodBid existing = bidsThisRound[index];
                if (existing != null && existing.Requester == bid.Requester &&
                    existing.Identity.SourceSimulationTick == bid.Identity.SourceSimulationTick)
                {
                    bidsThisRound[index] = bid;
                    return;
                }
            }

            bidsThisRound.Add(bid);
            SimulationLogManager.RecordEvent(
                "food.bid_submitted",
                "Food",
                "Info",
                bid.Requester,
                null,
                new SimulationLogField("sourceTick", bid.Identity.SourceSimulationTick),
                new SimulationLogField("hunger", bid.Hunger),
                new SimulationLogField("critical", bid.IsCritical),
                new SimulationLogField("preferenceRank", bid.Identity.BrainPreferenceRank));
        }

        public bool TryGetOffer(ColonistIdentity requester, long simulationTick, out FoodOffer offer)
        {
            offer = null;
            if (requester == null || !offers.TryGetValue(requester, out FoodOffer candidate))
                return false;

            if (!candidate.IsValidFor(simulationTick))
            {
                offers.Remove(requester);
                RecordOfferStale(candidate);
                return false;
            }

            offer = candidate;
            return true;
        }

        /// <summary>Read-only inspector access; never expires, removes, or logs an offer.</summary>
        public bool TryPeekOffer(ColonistIdentity requester, long simulationTick, out FoodOffer offer)
        {
            offer = null;
            return requester != null && offers.TryGetValue(requester, out offer) &&
                   offer != null && offer.IsValidFor(simulationTick);
        }

        public bool RemoveOffer(FoodOffer offer, bool accepted, string reason)
        {
            if (offer == null || offer.Requester == null ||
                !offers.TryGetValue(offer.Requester, out FoodOffer current) ||
                !ReferenceEquals(current, offer))
            {
                return false;
            }

            offers.Remove(offer.Requester);
            SimulationLogManager.RecordEvent(
                accepted ? "food.offer_accepted" : "food.offer_rejected",
                "Food",
                accepted ? "Info" : "Warning",
                offer.Requester,
                offer.Opportunity != null && offer.Opportunity.Service != null
                    ? offer.Opportunity.Service.Facility
                    : null,
                new SimulationLogField("reason", reason ?? string.Empty),
                new SimulationLogField("validTick", offer.ValidForSimulationTick));
            return true;
        }

        public bool TryReserveMeal(
            FoodOffer offer,
            ColonistActivityRunner runner,
            out FoodMealCommitment commitment)
        {
            commitment = null;
            if (offer == null || offer.Opportunity == null ||
                offer.Opportunity.Service == null || runner == null ||
                runner.CurrentFacility != offer.Opportunity.Service.Facility ||
                runner.CurrentReservation == null)
            {
                return false;
            }

            FoodServiceComponent service = offer.Opportunity.Service;
            if (!service.HasUsableFoodInventory ||
                !service.FoodInventory.Reserve(service.FoodResource, service.FoodPerMeal))
            {
                SimulationLogManager.RecordEvent(
                    "food.inventory_reservation_failed",
                    "Food",
                    "Warning",
                    offer.Requester,
                    service.Facility,
                    new SimulationLogField("reason", "no_inventory"),
                    new SimulationLogField("amount", service.FoodPerMeal));
                return false;
            }

            commitment = new FoodMealCommitment(
                offer.Requester,
                service,
                service.FoodInventory,
                service.FoodResource,
                service.FoodPerMeal)
            {
                Reserved = true
            };
            SimulationLogManager.RecordEvent(
                "food.inventory_reserved",
                "Food",
                "Info",
                offer.Requester,
                service.Facility,
                new SimulationLogField("amount", service.FoodPerMeal));
            return true;
        }

        public bool CommitMeal(FoodMealCommitment commitment)
        {
            if (commitment == null || !commitment.Reserved || commitment.Released)
                return commitment == null || commitment.Consumed;
            if (commitment.Consumed)
                return true;

            float withdrawn = commitment.Inventory != null
                ? commitment.Inventory.WithdrawReserved(commitment.Resource, commitment.Amount)
                : 0f;
            if (withdrawn + 0.0001f < commitment.Amount)
            {
                if (withdrawn > 0f)
                    commitment.Inventory.Add(commitment.Resource, withdrawn);
                ReleaseMeal(commitment);
                return false;
            }

            commitment.Consumed = true;
            commitment.Reserved = false;
            SimulationLogManager.RecordEvent(
                "food.meal_committed",
                "Food",
                "Info",
                commitment.Requester,
                commitment.Service != null ? commitment.Service.Facility : null,
                new SimulationLogField("amount", commitment.Amount));
            return true;
        }

        public void ReleaseMeal(FoodMealCommitment commitment)
        {
            if (commitment == null || commitment.Consumed || commitment.Released)
                return;

            if (commitment.Reserved && commitment.Inventory != null)
                commitment.Inventory.ReleaseReservation(commitment.Resource, commitment.Amount);
            commitment.Reserved = false;
            commitment.Released = true;
            SimulationLogManager.RecordEvent(
                "food.inventory_released",
                "Food",
                "Info",
                commitment.Requester,
                commitment.Service != null ? commitment.Service.Facility : null,
                new SimulationLogField("amount", commitment.Amount));
        }

        public void SimulationTick(float deltaGameHours)
        {
            long currentTick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick
                : 0L;

            // Offers from the preceding decision round are valid only for this Brain pass.
            List<ColonistIdentity> expired = null;
            foreach (KeyValuePair<ColonistIdentity, FoodOffer> pair in offers)
            {
                if (pair.Value == null || pair.Value.ValidForSimulationTick <= currentTick)
                {
                    if (expired == null)
                        expired = new List<ColonistIdentity>();
                    expired.Add(pair.Key);
                    if (pair.Value != null)
                        RecordOfferStale(pair.Value);
                }
            }
            if (expired != null)
                for (int index = 0; index < expired.Count; index++)
                    offers.Remove(expired[index]);

            ResolveBids(currentTick);
            bidsThisRound.Clear();
        }

        private void ResolveBids(long currentTick)
        {
            if (bidsThisRound.Count == 0)
                return;

            bidsThisRound.Sort(CompareBids);
            var offeredGroups = new HashSet<string>(StringComparer.Ordinal);
            var virtualStock = new Dictionary<FoodInventoryKey, float>();
            for (int index = 0; index < bidsThisRound.Count; index++)
            {
                FoodBid bid = bidsThisRound[index];
                if (bid == null || bid.Requester == null)
                    continue;

                foodQueryCount++;

                if (!TryFindFoodServiceForBid(
                        bid,
                        offeredGroups,
                        virtualStock,
                        out FoodServiceOpportunity opportunity,
                        out string noOfferReason))
                {
                    SimulationLogManager.RecordEvent(
                        "food.no_offer",
                        "Food",
                        "Info",
                        bid.Requester,
                        null,
                        new SimulationLogField("reason", noOfferReason));
                    continue;
                }

                string groupKey = ReservationGroupKey(opportunity.Service);
                offeredGroups.Add(groupKey);
                if (opportunity.Service.InventoryAccountingEnabled)
                {
                    FoodInventoryKey stockKey = new FoodInventoryKey(
                        opportunity.Service.FoodInventory,
                        opportunity.Service.FoodResource);
                    virtualStock.TryGetValue(stockKey, out float reservedForOffers);
                    virtualStock[stockKey] = reservedForOffers + opportunity.Service.FoodPerMeal;
                }

                FoodOffer offer = new FoodOffer(bid, opportunity, currentTick + 1L);
                offers[bid.Requester] = offer;
                foodSelectionCount++;
                SimulationLogManager.RecordEvent(
                    "food.offer_created",
                    "Food",
                    "Info",
                    bid.Requester,
                    opportunity.Service.Facility,
                    new SimulationLogField("validTick", currentTick + 1L),
                    new SimulationLogField("activityId", opportunity.Target.ActivityId));
            }
        }

        private bool TryFindFoodServiceForBid(
            FoodBid bid,
            HashSet<string> offeredGroups,
            Dictionary<FoodInventoryKey, float> virtualStock,
            out FoodServiceOpportunity opportunity,
            out string noOfferReason)
        {
            opportunity = null;
            PruneServices();
            noOfferReason = services.Count == 0 ? "no_registered_service" : "no_eligible_service";
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < services.Count; index++)
            {
                FoodServiceComponent candidate = services[index];
                foodCandidateEvaluationCount++;
                if (!TryEvaluateCandidate(
                        new FoodQuery(bid.Requester, bid.Position, bid.CurrentGameHour),
                        candidate,
                        out FoodServiceAccessMode accessMode,
                        out FacilityActivityBinding binding,
                        out string candidateFailure))
                {
                    SetMoreSpecificFailure(ref noOfferReason, candidateFailure);
                    continue;
                }

                string groupKey = ReservationGroupKey(candidate);
                if (offeredGroups.Contains(groupKey))
                {
                    SetMoreSpecificFailure(ref noOfferReason, "reservation_group_already_offered");
                    continue;
                }

                if (candidate.InventoryAccountingEnabled)
                {
                    FoodInventoryKey stockKey = new FoodInventoryKey(
                        candidate.FoodInventory,
                        candidate.FoodResource);
                    virtualStock.TryGetValue(stockKey, out float alreadyOffered);
                    if (candidate.FoodAvailable - alreadyOffered + 0.0001f < candidate.FoodPerMeal)
                    {
                        SetMoreSpecificFailure(ref noOfferReason, "no_inventory");
                        continue;
                    }
                }

                float distance = (binding.ApproachAnchor.position - bid.Position).sqrMagnitude;
                if (opportunity == null || distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     IsPreferredTieBreak(candidate, opportunity.Service)))
                {
                    bestDistance = distance;
                    opportunity = new FoodServiceOpportunity(
                        new ActivityTarget(candidate.Facility, candidate.EatActivityId),
                        candidate,
                        accessMode);
                    noOfferReason = string.Empty;
                }
            }

            return opportunity != null;
        }

        private static int CompareBids(FoodBid left, FoodBid right)
        {
            int critical = right.IsCritical.CompareTo(left.IsCritical);
            if (critical != 0)
                return critical;
            int hunger = right.Hunger.CompareTo(left.Hunger);
            if (hunger != 0)
                return hunger;
            int age = right.OutstandingNeedAge.CompareTo(left.OutstandingNeedAge);
            if (age != 0)
                return age;
            int rank = left.Identity.BrainPreferenceRank.CompareTo(right.Identity.BrainPreferenceRank);
            if (rank != 0)
                return rank;
            return string.CompareOrdinal(left.Requester.name, right.Requester.name);
        }

        private static string ReservationGroupKey(FoodServiceComponent service)
        {
            if (service == null || service.Facility == null ||
                !service.TryGetCachedEatBinding(out FacilityActivityBinding binding))
            {
                return string.Empty;
            }

            return service.Facility.GetEntityId().ToString() + ":" + binding.ReservationGroup;
        }

        private void RecordOfferStale(FoodOffer offer)
        {
            SimulationLogManager.RecordEvent(
                "food.offer_stale",
                "Food",
                "Info",
                offer.Requester,
                offer.Opportunity != null && offer.Opportunity.Service != null
                    ? offer.Opportunity.Service.Facility
                    : null,
                new SimulationLogField("validTick", offer.ValidForSimulationTick));
        }

        public bool TryFindFoodTarget(
            FoodQuery query,
            out ActivityTarget target)
        {
            if (TryFindFoodService(query, out FoodServiceOpportunity opportunity))
            {
                target = opportunity.Target;
                return target != null && target.IsConfigured;
            }

            target = null;
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
                foodCandidateEvaluationCount++;
                if (!TryEvaluateCandidate(
                        query,
                        candidate,
                        out FoodServiceAccessMode accessMode,
                        out FacilityActivityBinding binding,
                        out _))
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
            out FacilityActivityBinding binding,
            out string failureReason)
        {
            accessMode = FoodServiceAccessMode.Unavailable;
            binding = null;
            failureReason = "service_unconfigured";
            if (service == null || !service.TryGetCachedEatBinding(out binding))
                return false;

            if (service.Facility.IsReserved(binding.ReservationGroup))
            {
                failureReason = "activity_reserved";
                return false;
            }

            accessMode = service.EvaluateAccessFromCachedConfiguration(
                query.Seeker,
                query.CurrentGameHour);
            if (accessMode == FoodServiceAccessMode.Unavailable)
            {
                if (service.RequiresStaff && !service.HasActivePublicStaff(query.CurrentGameHour))
                    failureReason = service.AllowsSelfService(query.Seeker)
                        ? "staff_unavailable"
                        : "self_service_denied";
                else
                    failureReason = "service_unavailable";
                return false;
            }

            if (!service.HasAvailableMeal())
            {
                failureReason = "no_inventory";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static void SetMoreSpecificFailure(ref string currentReason, string candidateReason)
        {
            int currentPriority = FailureReasonPriority(currentReason);
            int candidatePriority = FailureReasonPriority(candidateReason);
            if (candidatePriority > currentPriority)
                currentReason = candidateReason;
        }

        private static int FailureReasonPriority(string reason)
        {
            switch (reason)
            {
                case "no_registered_service": return 0;
                case "no_eligible_service": return 1;
                case "service_unconfigured": return 2;
                case "service_unavailable": return 3;
                case "staff_unavailable":
                case "self_service_denied": return 4;
                case "activity_reserved":
                case "reservation_group_already_offered": return 5;
                case "no_inventory": return 6;
                default: return 0;
            }
        }

        private bool IsConfiguredService(FoodServiceComponent service)
        {
            return service != null && service.TryGetCachedEatBinding(out _) && service.HasUsableFoodInventory;
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

        private readonly struct FoodInventoryKey : IEquatable<FoodInventoryKey>
        {
            public FoodInventoryKey(InventoryComponent inventory, ResourceDefinition resource)
            {
                Inventory = inventory;
                Resource = resource;
            }

            private InventoryComponent Inventory { get; }
            private ResourceDefinition Resource { get; }

            public bool Equals(FoodInventoryKey other)
            {
                return ReferenceEquals(Inventory, other.Inventory) &&
                       ReferenceEquals(Resource, other.Resource);
            }

            public override bool Equals(object obj)
            {
                return obj is FoodInventoryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Inventory != null ? Inventory.GetEntityId().GetHashCode() : 0) * 397) ^
                           (Resource != null ? Resource.GetEntityId().GetHashCode() : 0);
                }
            }
        }
    }
}
