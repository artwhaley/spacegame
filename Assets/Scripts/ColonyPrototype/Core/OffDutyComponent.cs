using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    // Which soft discretionary drive a colonist is currently trying to satisfy.
    // This is deliberately not an Active/Passive classification: the recovery rates
    // authored on the activity are the behavioural truth.
    public enum OffDutyDrive
    {
        Stimulation,
        Relaxation
    }

    [Serializable]
    public sealed class OffDutyActivityBinding
    {
        [SerializeField] private string activityId;
        [SerializeField, Min(0.0001f)] private float plannedDurationGameHours = 1f;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool requiresStaff;
        [SerializeField] private WorkplaceComponent requiredWorkplace;
        [SerializeField] private JobRoleDefinition requiredRole;
        [SerializeField, Min(1)] private int minimumActiveWorkers = 1;
        [SerializeField, Min(0f)] private float cooldownGameHours = 12f;
        [SerializeField] private string cooldownKey;
        [SerializeField, Min(0f)] private float stimulationRecoveryPerGameHour;
        [SerializeField, Min(0f)] private float relaxationRecoveryPerGameHour;

        public string ActivityId => activityId;
        public float PlannedDurationGameHours => plannedDurationGameHours;
        public bool Enabled => enabled;
        public bool RequiresStaff => requiresStaff;
        public WorkplaceComponent RequiredWorkplace => requiredWorkplace;
        public JobRoleDefinition RequiredRole => requiredRole;
        public int MinimumActiveWorkers => minimumActiveWorkers;
        public float CooldownGameHours =>
            IsFinite(cooldownGameHours) ? Mathf.Max(0f, cooldownGameHours) : 0f;
        public float StimulationRecoveryPerGameHour =>
            IsFinite(stimulationRecoveryPerGameHour)
                ? Mathf.Max(0f, stimulationRecoveryPerGameHour)
                : 0f;
        public float RelaxationRecoveryPerGameHour =>
            IsFinite(relaxationRecoveryPerGameHour)
                ? Mathf.Max(0f, relaxationRecoveryPerGameHour)
                : 0f;

        // Cooldown is keyed by authored semantic identity, not by facility instance, so two
        // bowling lanes can share one 'bowling' cooldown instead of letting a colonist dodge
        // the cooldown by walking to the other lane.
        public string CooldownKey =>
            string.IsNullOrWhiteSpace(cooldownKey)
                ? (activityId ?? string.Empty)
                : cooldownKey.Trim();

        public bool HasCooldown => CooldownGameHours > 0f;

        public bool Satisfies(OffDutyDrive drive)
        {
            return drive == OffDutyDrive.Stimulation
                ? StimulationRecoveryPerGameHour > 0f
                : RelaxationRecoveryPerGameHour > 0f;
        }

        public bool IsStructurallyValid =>
            !string.IsNullOrWhiteSpace(activityId) &&
            !float.IsNaN(plannedDurationGameHours) &&
            !float.IsInfinity(plannedDurationGameHours) &&
            plannedDurationGameHours > 0f &&
            minimumActiveWorkers > 0;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableFacility))]
    public sealed class OffDutyComponent : MonoBehaviour
    {
        [SerializeField] private InteractableFacility facility;
        [SerializeField] private OffDutyActivityBinding[] activities =
            Array.Empty<OffDutyActivityBinding>();

        [NonSerialized]
        private readonly List<CachedActivityMetadata> cachedActivityMetadata =
            new List<CachedActivityMetadata>();

        [NonSerialized] private bool staticBindingCacheInitialized;
        [NonSerialized] private InteractableFacility cachedFacility;
        [NonSerialized] private OffDutyActivityBinding[] cachedActivitiesSource;

        public InteractableFacility Facility
        {
            get
            {
                ResolveFacility();
                return facility;
            }
        }

        public IReadOnlyList<OffDutyActivityBinding> Activities =>
            activities ?? Array.Empty<OffDutyActivityBinding>();

        public bool TryGetActivity(
            string activityId,
            out OffDutyActivityBinding activity)
        {
            activity = null;
            IReadOnlyList<OffDutyActivityBinding> configuredActivities = Activities;
            for (int index = 0; index < configuredActivities.Count; index++)
            {
                OffDutyActivityBinding candidate = configuredActivities[index];
                if (candidate != null &&
                    string.Equals(candidate.ActivityId, activityId, StringComparison.Ordinal))
                {
                    activity = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsConfigured(OffDutyActivityBinding activity)
        {
            return TryGetCachedBinding(activity, out _);
        }

        public bool IsLive(OffDutyActivityBinding activity)
        {
            return enabled && activity != null && activity.Enabled &&
                   TryGetCachedBinding(activity, out _);
        }

        public bool IsDiscoverable(OffDutyActivityBinding activity)
        {
            return enabled &&
                   activity != null &&
                   activity.Enabled &&
                   TryGetCachedBinding(activity, out FacilityActivityBinding binding) &&
                   !Facility.IsReserved(binding.ReservationGroup);
        }

        /// <summary>
        /// Static recreation binding metadata is prepared once at authoring/enable time. The
        /// manager uses this path during candidate evaluation so it does not repeatedly call
        /// IsConfigured and rescan the facility's activity array for every colonist.
        /// </summary>
        public bool TryGetCachedBinding(
            OffDutyActivityBinding activity,
            out FacilityActivityBinding binding)
        {
            RefreshCacheIfNeeded();
            binding = null;
            CachedActivityMetadata metadata = FindCachedMetadata(activity);
            if (metadata == null || !metadata.StructurallyConfigured)
                return false;

            binding = metadata.FacilityBinding;
            return binding != null;
        }

        public void RefreshStaticBindingMetadata()
        {
            ResolveFacility();
            cachedFacility = facility;
            cachedActivitiesSource = activities;
            cachedActivityMetadata.Clear();
            IReadOnlyList<OffDutyActivityBinding> configuredActivities = Activities;
            for (int index = 0; index < configuredActivities.Count; index++)
            {
                OffDutyActivityBinding activity = configuredActivities[index];
                FacilityActivityBinding facilityBinding = null;
                bool structurallyConfigured = activity != null &&
                    activity.IsStructurallyValid &&
                    cachedFacility != null &&
                    !string.IsNullOrWhiteSpace(activity.ActivityId) &&
                    cachedFacility.TryGetBinding(activity.ActivityId, out facilityBinding) &&
                    facilityBinding != null &&
                    facilityBinding.ExternallyRequestable &&
                    !string.IsNullOrWhiteSpace(facilityBinding.ReservationGroup) &&
                    facilityBinding.ApproachAnchor != null &&
                    (!activity.RequiresStaff ||
                     (activity.RequiredWorkplace != null &&
                      activity.RequiredRole != null &&
                      activity.RequiredWorkplace.Facility != null &&
                      activity.RequiredWorkplace.TryGetRoleBinding(activity.RequiredRole, out _)));
                cachedActivityMetadata.Add(new CachedActivityMetadata(
                    activity,
                    facilityBinding,
                    structurallyConfigured));
            }

            staticBindingCacheInitialized = true;
        }

        private void Awake()
        {
            ResolveFacility();
            RefreshStaticBindingMetadata();
        }

        private void OnEnable()
        {
            RefreshStaticBindingMetadata();
            OffDutyManager.Instance?.Register(this);
        }

        private void Start()
        {
            OffDutyManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            staticBindingCacheInitialized = false;
            OffDutyManager.Instance?.Unregister(this);
        }

        private void OnValidate()
        {
            ResolveFacility();
            RefreshStaticBindingMetadata();
        }

        private void RefreshCacheIfNeeded()
        {
            ResolveFacility();
            if (!staticBindingCacheInitialized ||
                cachedFacility != facility ||
                !ReferenceEquals(cachedActivitiesSource, activities))
                RefreshStaticBindingMetadata();
        }

        private CachedActivityMetadata FindCachedMetadata(OffDutyActivityBinding activity)
        {
            for (int index = 0; index < cachedActivityMetadata.Count; index++)
            {
                CachedActivityMetadata metadata = cachedActivityMetadata[index];
                if (metadata.Activity == activity)
                    return metadata;
            }

            return null;
        }

        private void ResolveFacility()
        {
            if (facility == null)
                facility = GetComponent<InteractableFacility>();
        }

        private sealed class CachedActivityMetadata
        {
            public CachedActivityMetadata(
                OffDutyActivityBinding activity,
                FacilityActivityBinding facilityBinding,
                bool structurallyConfigured)
            {
                Activity = activity;
                FacilityBinding = facilityBinding;
                StructurallyConfigured = structurallyConfigured;
            }

            public OffDutyActivityBinding Activity { get; }
            public FacilityActivityBinding FacilityBinding { get; }
            public bool StructurallyConfigured { get; }
        }
    }
}
