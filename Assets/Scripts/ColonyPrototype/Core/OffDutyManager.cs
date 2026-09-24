using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public sealed class OffDutyOpportunity
    {
        public OffDutyOpportunity(
            ActivityTarget target,
            float plannedDurationGameHours,
            OffDutyComponent provider,
            OffDutyActivityBinding activity)
        {
            Target = target;
            PlannedDurationGameHours = plannedDurationGameHours;
            Provider = provider;
            Activity = activity;
        }

        public ActivityTarget Target { get; }
        public float PlannedDurationGameHours { get; }
        public OffDutyComponent Provider { get; }
        public OffDutyActivityBinding Activity { get; }
        public float DurationGameHours => PlannedDurationGameHours;
    }

    public sealed class OffDutyQuery
    {
        public OffDutyQuery(Vector3 seekerPosition, float maximumAllowedDurationGameHours)
            : this(
                null,
                seekerPosition,
                maximumAllowedDurationGameHours,
                null,
                null,
                0f)
        {
        }

        public OffDutyQuery(
            ColonistIdentity seeker,
            Vector3 seekerPosition,
            float maximumAllowedDurationGameHours,
            OffDutyDrive? desiredDrive,
            OffDutyCompletionHistory completionHistory,
            float currentAbsoluteGameHour)
        {
            Seeker = seeker;
            SeekerPosition = seekerPosition;
            MaximumAllowedDurationGameHours = maximumAllowedDurationGameHours;
            DesiredDrive = desiredDrive;
            CompletionHistory = completionHistory;
            CurrentAbsoluteGameHour = currentAbsoluteGameHour;
        }

        public ColonistIdentity Seeker { get; }
        public Vector3 SeekerPosition { get; }
        public float MaximumAllowedDurationGameHours { get; }

        // A null drive means "no drive constraint" (inspection and legacy callers).
        // ColonistBrain always supplies the drive it is trying to satisfy.
        public OffDutyDrive? DesiredDrive { get; }
        public OffDutyCompletionHistory CompletionHistory { get; }
        public float CurrentAbsoluteGameHour { get; }
    }

    public sealed class OffDutySearchReport
    {
        public int EvaluatedActivities { get; internal set; }
        public int DriveMatchingActivities { get; internal set; }
        public int ExcludedByAvailability { get; internal set; }
        public int ExcludedByDuration { get; internal set; }
        public int ExcludedByStaff { get; internal set; }
        public int ExcludedByCooldown { get; internal set; }
        public bool HasSelection { get; internal set; }

        /// <summary>
        /// A truthful explanation for an empty result: only reported as 'cooldown' when every
        /// drive-matching activity was excluded by cooldown, otherwise the aggregate reason.
        /// </summary>
        public string NoTargetReason
        {
            get
            {
                if (HasSelection)
                    return "selected";

                if (DriveMatchingActivities > 0 &&
                    ExcludedByCooldown == DriveMatchingActivities)
                {
                    return "cooldown";
                }

                return "no_fitting_opportunity";
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class OffDutyManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public static OffDutyManager Instance { get; private set; }

        [SerializeField] private List<OffDutyComponent> providers =
            new List<OffDutyComponent>();
        [SerializeField, Min(0)]
        private int declinedOfferSuppressionRounds = 6;

        [NonSerialized] private readonly List<OffDutyBid> bidsThisRound = new List<OffDutyBid>();
        [NonSerialized] private readonly Dictionary<ColonistIdentity, OffDutyOffer> offers =
            new Dictionary<ColonistIdentity, OffDutyOffer>();
        [NonSerialized] private readonly Dictionary<DeclineKey, long> declinedUntilTick =
            new Dictionary<DeclineKey, long>();
        private long opportunityQueryCount;
        private long opportunityCandidateEvaluationCount;

        public IReadOnlyList<OffDutyComponent> Providers =>
            providers ?? (IReadOnlyList<OffDutyComponent>)Array.Empty<OffDutyComponent>();
        public long OpportunityQueryCount => opportunityQueryCount;
        public long OpportunityCandidateEvaluationCount => opportunityCandidateEvaluationCount;
        public int SimulationTickPriority => 150;
        public IReadOnlyList<OffDutyBid> BidsThisRound => bidsThisRound;
        public IReadOnlyCollection<OffDutyOffer> Offers => offers.Values;

        public void ResetDiagnostics()
        {
            opportunityQueryCount = 0;
            opportunityCandidateEvaluationCount = 0;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active OffDutyManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            RegisterExistingProviders();
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
            declinedUntilTick.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(OffDutyComponent provider)
        {
            if (provider == null)
                return;

            PruneProviders();
            if (!providers.Contains(provider))
                providers.Add(provider);
        }

        public void Unregister(OffDutyComponent provider)
        {
            if (provider != null && providers != null)
                providers.Remove(provider);
        }

        public void SubmitBid(OffDutyBid bid)
        {
            if (bid == null || bid.Requester == null)
                return;

            for (int index = bidsThisRound.Count - 1; index >= 0; index--)
            {
                OffDutyBid existing = bidsThisRound[index];
                if (existing != null && existing.Requester == bid.Requester &&
                    existing.Identity.SourceSimulationTick == bid.Identity.SourceSimulationTick &&
                    existing.DesiredDrive == bid.DesiredDrive)
                {
                    bidsThisRound[index] = bid;
                    return;
                }
            }

            bidsThisRound.Add(bid);
        }

        public bool TryGetOffer(ColonistIdentity requester, long simulationTick, out OffDutyOffer offer)
        {
            offer = null;
            if (requester == null || !offers.TryGetValue(requester, out OffDutyOffer candidate))
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
        public bool TryPeekOffer(ColonistIdentity requester, long simulationTick, out OffDutyOffer offer)
        {
            offer = null;
            return requester != null && offers.TryGetValue(requester, out offer) &&
                   offer != null && offer.IsValidFor(simulationTick);
        }

        public bool RemoveOffer(OffDutyOffer offer, bool accepted, string reason)
        {
            if (offer == null || offer.Requester == null ||
                !offers.TryGetValue(offer.Requester, out OffDutyOffer current) ||
                !ReferenceEquals(current, offer))
            {
                return false;
            }

            offers.Remove(offer.Requester);
            if (!accepted)
            {
                long currentTick = SimulationManager.Instance != null
                    ? SimulationManager.Instance.CurrentTick
                    : offer.ValidForSimulationTick;
                DeclineKey key = new DeclineKey(
                    offer.Requester,
                    offer.Opportunity != null ? offer.Opportunity.Provider : null,
                    offer.Opportunity != null && offer.Opportunity.Activity != null
                        ? offer.Opportunity.Activity.ActivityId
                        : string.Empty);
                declinedUntilTick[key] = currentTick + Mathf.Max(0, declinedOfferSuppressionRounds);
            }

            SimulationLogManager.RecordEvent(
                accepted ? "offduty.offer_accepted" : "offduty.offer_rejected",
                "OffDuty",
                accepted ? "Info" : "Warning",
                offer.Requester,
                offer.Opportunity != null && offer.Opportunity.Provider != null
                    ? offer.Opportunity.Provider.Facility
                    : null,
                new SimulationLogField("reason", reason ?? string.Empty),
                new SimulationLogField("validTick", offer.ValidForSimulationTick));
            return true;
        }

        public void SimulationTick(float deltaGameHours)
        {
            long currentTick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick
                : 0L;
            List<ColonistIdentity> expired = null;
            foreach (KeyValuePair<ColonistIdentity, OffDutyOffer> pair in offers)
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

            List<DeclineKey> expiredSuppressions = null;
            foreach (KeyValuePair<DeclineKey, long> pair in declinedUntilTick)
            {
                if (pair.Value <= currentTick)
                {
                    if (expiredSuppressions == null)
                        expiredSuppressions = new List<DeclineKey>();
                    expiredSuppressions.Add(pair.Key);
                }
            }
            if (expiredSuppressions != null)
                for (int index = 0; index < expiredSuppressions.Count; index++)
                    declinedUntilTick.Remove(expiredSuppressions[index]);

            ResolveBids(currentTick);
            bidsThisRound.Clear();
        }

        private void ResolveBids(long currentTick)
        {
            if (bidsThisRound.Count == 0)
                return;

            bidsThisRound.Sort(CompareBids);
            var offeredGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < bidsThisRound.Count; index++)
            {
                OffDutyBid bid = bidsThisRound[index];
                if (bid == null || bid.Requester == null || offers.ContainsKey(bid.Requester))
                    continue;

                opportunityQueryCount++;

                if (!TryFindOpportunityForBid(
                        bid,
                        offeredGroups,
                        out OffDutyOpportunity opportunity))
                    continue;

                offeredGroups.Add(ReservationGroupKey(opportunity));
                OffDutyOffer offer = new OffDutyOffer(bid, opportunity, currentTick + 1L);
                offers[bid.Requester] = offer;
                SimulationLogManager.RecordEvent(
                    "offduty.offer_created",
                    "OffDuty",
                    "Info",
                    bid.Requester,
                    opportunity.Provider.Facility,
                    new SimulationLogField("validTick", currentTick + 1L),
                    new SimulationLogField("activityId", opportunity.Target.ActivityId),
                    new SimulationLogField("sourceTick", bid.Identity.SourceSimulationTick),
                    new SimulationLogField("drive", bid.DesiredDrive.ToString()),
                    new SimulationLogField("preferenceRank", bid.Identity.BrainPreferenceRank),
                    new SimulationLogField(
                        "maximumSafeDurationGameHours",
                        bid.MaximumSafeDurationGameHours),
                    new SimulationLogField(
                        "plannedDurationGameHours",
                        opportunity.PlannedDurationGameHours));
            }
        }

        private bool TryFindOpportunityForBid(
            OffDutyBid bid,
            HashSet<string> offeredGroups,
            out OffDutyOpportunity opportunity)
        {
            opportunity = null;
            PruneProviders();
            float bestDistance = float.PositiveInfinity;
            for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                OffDutyComponent provider = providers[providerIndex];
                IReadOnlyList<OffDutyActivityBinding> activities = provider.Activities;
                for (int activityIndex = 0; activityIndex < activities.Count; activityIndex++)
                {
                    opportunityCandidateEvaluationCount++;
                    OffDutyActivityBinding activity = activities[activityIndex];
                    if (activity == null || !activity.Satisfies(bid.DesiredDrive) ||
                        IsSuppressed(bid.Requester, provider, activity) ||
                        !provider.TryGetCachedBinding(activity, out FacilityActivityBinding binding) ||
                        provider.Facility.IsReserved(binding.ReservationGroup) ||
                        !provider.IsDiscoverable(activity) ||
                        activity.PlannedDurationGameHours > bid.MaximumSafeDurationGameHours ||
                        IsOnCooldown(activity, new OffDutyQuery(
                            bid.Requester,
                            bid.Position,
                            bid.MaximumSafeDurationGameHours,
                            bid.DesiredDrive,
                            bid.CompletionHistory,
                            bid.CurrentGameHour)) ||
                        !HasRequiredStaff(activity))
                    {
                        continue;
                    }

                    string groupKey = provider.Facility.GetEntityId().ToString() + ":" + binding.ReservationGroup;
                    if (offeredGroups.Contains(groupKey))
                        continue;

                    float distance = (binding.ApproachAnchor.position - bid.Position).sqrMagnitude;
                    if (opportunity == null || distance < bestDistance ||
                        (Mathf.Approximately(distance, bestDistance) &&
                         IsPreferredTieBreak(provider, activity, opportunity)))
                    {
                        bestDistance = distance;
                        opportunity = new OffDutyOpportunity(
                            new ActivityTarget(provider.Facility, activity.ActivityId),
                            activity.PlannedDurationGameHours,
                            provider,
                            activity);
                    }
                }
            }

            return opportunity != null;
        }

        private bool IsSuppressed(
            ColonistIdentity requester,
            OffDutyComponent provider,
            OffDutyActivityBinding activity)
        {
            DeclineKey key = new DeclineKey(requester, provider, activity.ActivityId);
            return declinedUntilTick.TryGetValue(key, out long until) &&
                   (SimulationManager.Instance == null ||
                    until > SimulationManager.Instance.CurrentTick);
        }

        private static int CompareBids(OffDutyBid left, OffDutyBid right)
        {
            int requester = string.CompareOrdinal(left.Requester.name, right.Requester.name);
            if (requester != 0)
                return requester;
            return left.Identity.BrainPreferenceRank.CompareTo(right.Identity.BrainPreferenceRank);
        }

        private static string ReservationGroupKey(OffDutyOpportunity opportunity)
        {
            if (opportunity == null || opportunity.Provider == null ||
                opportunity.Provider.Facility == null || opportunity.Activity == null ||
                !opportunity.Provider.TryGetCachedBinding(
                    opportunity.Activity,
                    out FacilityActivityBinding binding))
            {
                return string.Empty;
            }

            return opportunity.Provider.Facility.GetEntityId().ToString() + ":" +
                   binding.ReservationGroup;
        }

        private static void RecordOfferStale(OffDutyOffer offer)
        {
            SimulationLogManager.RecordEvent(
                "offduty.offer_stale",
                "OffDuty",
                "Info",
                offer.Requester,
                offer.Opportunity != null && offer.Opportunity.Provider != null
                    ? offer.Opportunity.Provider.Facility
                    : null,
                new SimulationLogField("validTick", offer.ValidForSimulationTick));
        }

        public bool TryFindOpportunity(
            OffDutyQuery query,
            out OffDutyOpportunity opportunity)
        {
            return TryFindOpportunity(query, out opportunity, out _);
        }

        public bool TryFindOpportunity(
            OffDutyQuery query,
            out OffDutyOpportunity opportunity,
            out OffDutySearchReport report)
        {
            opportunity = null;
            report = new OffDutySearchReport();
            if (query == null ||
                float.IsNaN(query.MaximumAllowedDurationGameHours) ||
                query.MaximumAllowedDurationGameHours <= 0f)
            {
                return false;
            }

            PruneProviders();
            float bestDistance = float.PositiveInfinity;
            for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                OffDutyComponent provider = providers[providerIndex];
                IReadOnlyList<OffDutyActivityBinding> activities = provider.Activities;
                for (int activityIndex = 0; activityIndex < activities.Count; activityIndex++)
                {
                    OffDutyActivityBinding activity = activities[activityIndex];
                    report.EvaluatedActivities++;
                    opportunityCandidateEvaluationCount++;

                    if (query.DesiredDrive.HasValue &&
                        !activity.Satisfies(query.DesiredDrive.Value))
                    {
                        continue;
                    }

                    report.DriveMatchingActivities++;

                    if (IsOnCooldown(activity, query))
                    {
                        report.ExcludedByCooldown++;
                        continue;
                    }

                    if (!provider.IsDiscoverable(activity))
                    {
                        report.ExcludedByAvailability++;
                        continue;
                    }

                    if (activity.PlannedDurationGameHours >
                        query.MaximumAllowedDurationGameHours)
                    {
                        report.ExcludedByDuration++;
                        continue;
                    }

                    if (!HasRequiredStaff(activity))
                    {
                        report.ExcludedByStaff++;
                        continue;
                    }

                    if (!provider.TryGetCachedBinding(
                            activity,
                            out FacilityActivityBinding facilityBinding))
                    {
                        report.ExcludedByAvailability++;
                        continue;
                    }

                    float distance =
                        (facilityBinding.ApproachAnchor.position - query.SeekerPosition).sqrMagnitude;
                    if (opportunity == null ||
                        distance < bestDistance ||
                        (Mathf.Approximately(distance, bestDistance) &&
                         IsPreferredTieBreak(provider, activity, opportunity)))
                    {
                        bestDistance = distance;
                        opportunity = new OffDutyOpportunity(
                            new ActivityTarget(provider.Facility, activity.ActivityId),
                            activity.PlannedDurationGameHours,
                            provider,
                            activity);
                    }
                }
            }

            report.HasSelection = opportunity != null;
            return opportunity != null;
        }

        public bool TryFindOpportunity(
            Vector3 seekerPosition,
            float maximumAllowedDurationGameHours,
            out OffDutyOpportunity opportunity)
        {
            return TryFindOpportunity(
                new OffDutyQuery(seekerPosition, maximumAllowedDurationGameHours),
                out opportunity);
        }

        private static bool IsOnCooldown(
            OffDutyActivityBinding activity,
            OffDutyQuery query)
        {
            if (query.CompletionHistory == null || !activity.HasCooldown)
                return false;

            return query.CompletionHistory.IsOnCooldown(
                activity.CooldownKey,
                activity.CooldownGameHours,
                query.CurrentAbsoluteGameHour);
        }

        private bool HasRequiredStaff(OffDutyActivityBinding activity)
        {
            if (!activity.RequiresStaff)
                return true;

            return WorkforceManager.Instance != null &&
                   WorkforceManager.Instance.HasEnoughActiveWorkers(
                       activity.RequiredWorkplace,
                       activity.RequiredRole,
                       activity.MinimumActiveWorkers,
                       activity.PlannedDurationGameHours,
                       SimulationManager.Instance != null
                           ? SimulationManager.Instance.CurrentGameHour
                           : 0f);
        }

        private static bool IsPreferredTieBreak(
            OffDutyComponent provider,
            OffDutyActivityBinding activity,
            OffDutyOpportunity current)
        {
            string candidateName = provider.name + "/" + activity.ActivityId;
            string currentName = current.Provider.name + "/" + current.Activity.ActivityId;
            return string.CompareOrdinal(candidateName, currentName) < 0;
        }

        private void RegisterExistingProviders()
        {
            OffDutyComponent[] existing = FindObjectsByType<OffDutyComponent>(FindObjectsInactive.Exclude);
            for (int index = 0; index < existing.Length; index++)
                Register(existing[index]);
        }

        private void PruneProviders()
        {
            if (providers == null)
                providers = new List<OffDutyComponent>();

            for (int index = providers.Count - 1; index >= 0; index--)
            {
                if (providers[index] == null)
                    providers.RemoveAt(index);
            }
        }

        private readonly struct DeclineKey : IEquatable<DeclineKey>
        {
            public DeclineKey(
                ColonistIdentity requester,
                OffDutyComponent provider,
                string activityId)
            {
                Requester = requester;
                Provider = provider;
                ActivityId = activityId ?? string.Empty;
            }

            private ColonistIdentity Requester { get; }
            private OffDutyComponent Provider { get; }
            private string ActivityId { get; }

            public bool Equals(DeclineKey other)
            {
                return ReferenceEquals(Requester, other.Requester) &&
                       ReferenceEquals(Provider, other.Provider) &&
                       string.Equals(ActivityId, other.ActivityId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is DeclineKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (((Requester != null ? Requester.GetEntityId().GetHashCode() : 0) * 397) ^
                            (Provider != null ? Provider.GetEntityId().GetHashCode() : 0)) * 397 ^
                           (ActivityId != null ? ActivityId.GetHashCode() : 0);
                }
            }
        }
    }
}
