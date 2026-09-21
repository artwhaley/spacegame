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
        {
            SeekerPosition = seekerPosition;
            MaximumAllowedDurationGameHours = maximumAllowedDurationGameHours;
        }

        public Vector3 SeekerPosition { get; }
        public float MaximumAllowedDurationGameHours { get; }
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
            opportunity = null;
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
                    if (!provider.IsDiscoverable(activity) ||
                        activity.PlannedDurationGameHours > query.MaximumAllowedDurationGameHours ||
                        !HasRequiredStaff(activity))
                    {
                        continue;
                    }

                    if (!provider.Facility.TryGetBinding(
                            activity.ActivityId,
                            out FacilityActivityBinding facilityBinding))
                    {
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
