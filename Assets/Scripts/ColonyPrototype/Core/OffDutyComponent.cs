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
            if (activity == null || !activity.IsStructurallyValid || Facility == null)
                return false;

            if (!Facility.TryGetBinding(activity.ActivityId, out FacilityActivityBinding facilityBinding))
                return false;

            if (activity.RequiresStaff &&
                (activity.RequiredWorkplace == null ||
                 activity.RequiredRole == null ||
                 !activity.RequiredWorkplace.TryGetRoleBinding(activity.RequiredRole, out _)))
            {
                return false;
            }

            return facilityBinding.ExternallyRequestable &&
                   !string.IsNullOrWhiteSpace(facilityBinding.ReservationGroup) &&
                   facilityBinding.ApproachAnchor != null;
        }

        public bool IsLive(OffDutyActivityBinding activity)
        {
            return enabled && activity != null && activity.Enabled && IsConfigured(activity);
        }

        public bool IsDiscoverable(OffDutyActivityBinding activity)
        {
            return enabled &&
                   activity != null &&
                   activity.Enabled &&
                   IsConfigured(activity) &&
                   Facility.TryGetBinding(activity.ActivityId, out FacilityActivityBinding binding) &&
                   !Facility.IsReserved(binding.ReservationGroup);
        }

        private void Awake()
        {
            ResolveFacility();
        }

        private void OnEnable()
        {
            OffDutyManager.Instance?.Register(this);
        }

        private void Start()
        {
            OffDutyManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            OffDutyManager.Instance?.Unregister(this);
        }

        private void OnValidate()
        {
            ResolveFacility();
        }

        private void ResolveFacility()
        {
            if (facility == null)
                facility = GetComponent<InteractableFacility>();
        }
    }
}
