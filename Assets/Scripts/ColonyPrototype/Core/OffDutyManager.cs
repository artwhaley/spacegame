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
    public sealed class OffDutyManager : MonoBehaviour
    {
        public static OffDutyManager Instance { get; private set; }

        [SerializeField] private List<OffDutyComponent> providers =
            new List<OffDutyComponent>();

        public IReadOnlyList<OffDutyComponent> Providers =>
            providers ?? (IReadOnlyList<OffDutyComponent>)Array.Empty<OffDutyComponent>();

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

                    if (!provider.Facility.TryGetBinding(
                            activity.ActivityId,
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
    }
}
