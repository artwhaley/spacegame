using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum WorkplaceExecutionMode
    {
        FacilityActivity,
        MobileDuty
    }

    [Serializable]
    public sealed class WorkplaceRoleBinding
    {
        [SerializeField]
        private JobRoleDefinition role;

        [SerializeField]
        private string activityId;

        [SerializeField, Min(1)]
        private int maximumConcurrentScheduledWorkers = 1;

        public JobRoleDefinition Role => role;
        public string ActivityId => activityId;
        public int MaximumConcurrentScheduledWorkers =>
            maximumConcurrentScheduledWorkers;

        public WorkplaceRoleBinding()
        {
        }

        public WorkplaceRoleBinding(JobRoleDefinition role, string activityId, int capacity)
        {
            this.role = role;
            this.activityId = activityId ?? string.Empty;
            maximumConcurrentScheduledWorkers = Mathf.Max(1, capacity);
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorkplaceComponent : MonoBehaviour
    {
        [SerializeField]
        private WorkplaceExecutionMode executionMode = WorkplaceExecutionMode.FacilityActivity;

        [SerializeField]
        private InteractableFacility facility;

        [SerializeField]
        private Transform dutyAnchor;

        [SerializeField]
        private WorkplaceRoleBinding[] roles = Array.Empty<WorkplaceRoleBinding>();

        public InteractableFacility Facility
        {
            get
            {
                ResolveFacility();
                return facility;
            }
        }

        public WorkplaceExecutionMode ExecutionMode => executionMode;
        public Transform DutyAnchor => dutyAnchor;

        public IReadOnlyList<WorkplaceRoleBinding> Roles =>
            roles ?? Array.Empty<WorkplaceRoleBinding>();

        private void Awake()
        {
            ResolveFacility();
        }

        private void OnValidate()
        {
            ResolveFacility();
            ValidateAuthoring();
        }

        public bool OffersRole(JobRoleDefinition role)
        {
            return TryGetRoleBinding(role, out _);
        }

        public bool TryGetRoleBinding(
            JobRoleDefinition role,
            out WorkplaceRoleBinding binding)
        {
            binding = null;
            if (role == null)
                return false;

            WorkplaceRoleBinding match = null;
            IReadOnlyList<WorkplaceRoleBinding> configuredRoles = Roles;
            for (int index = 0; index < configuredRoles.Count; index++)
            {
                WorkplaceRoleBinding candidate = configuredRoles[index];
                if (candidate == null || candidate.Role != role)
                    continue;

                if (match != null || !IsBindingUsable(candidate))
                    return false;

                match = candidate;
            }

            binding = match;
            return binding != null;
        }

        private bool IsBindingUsable(WorkplaceRoleBinding binding)
        {
            if (binding.Role == null || binding.MaximumConcurrentScheduledWorkers < 1)
                return false;
            if (executionMode == WorkplaceExecutionMode.MobileDuty)
                return dutyAnchor != null;
            return !string.IsNullOrWhiteSpace(binding.ActivityId) &&
                   Facility != null &&
                   Facility.TryGetBinding(binding.ActivityId, out _);
        }

        public void ConfigureMobileDuty(
            JobRoleDefinition role,
            int capacity,
            Transform anchor)
        {
            executionMode = WorkplaceExecutionMode.MobileDuty;
            facility = null;
            dutyAnchor = anchor;
            roles = role == null
                ? Array.Empty<WorkplaceRoleBinding>()
                : new[] { new WorkplaceRoleBinding(role, string.Empty, capacity) };
        }

        public void ConfigureFacilityActivity(
            InteractableFacility activeFacility,
            WorkplaceRoleBinding[] bindings)
        {
            executionMode = WorkplaceExecutionMode.FacilityActivity;
            facility = activeFacility;
            roles = bindings ?? Array.Empty<WorkplaceRoleBinding>();
        }

        private void ResolveFacility()
        {
            if (facility == null)
                facility = GetComponent<InteractableFacility>();
        }

        private void ValidateAuthoring()
        {
            if (executionMode == WorkplaceExecutionMode.MobileDuty)
            {
                if (dutyAnchor == null)
                    Debug.LogError($"{name}: mobile-duty workplace needs a duty anchor.", this);
            }
            else if (Facility == null)
            {
                if (Roles.Count == 0)
                    return;
                Debug.LogError(
                    $"{name}: WorkplaceComponent requires a sibling InteractableFacility.",
                    this);
                return;
            }

            HashSet<JobRoleDefinition> seenRoles =
                new HashSet<JobRoleDefinition>();
            IReadOnlyList<WorkplaceRoleBinding> configuredRoles = Roles;
            for (int index = 0; index < configuredRoles.Count; index++)
            {
                WorkplaceRoleBinding binding = configuredRoles[index];
                if (binding == null)
                {
                    Debug.LogError(
                        $"{name}: workplace role binding {index} is null.",
                        this);
                    continue;
                }

                if (binding.Role == null)
                {
                    Debug.LogError(
                        $"{name}: workplace role binding {index} has no role.",
                        this);
                }
                else if (!seenRoles.Add(binding.Role))
                {
                    Debug.LogError(
                        $"{name}: duplicate workplace role {binding.Role.name}.",
                        this);
                }

                if (executionMode == WorkplaceExecutionMode.MobileDuty)
                {
                    if (binding.MaximumConcurrentScheduledWorkers < 1)
                        Debug.LogError($"{name}: mobile role capacity must be at least 1.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.ActivityId))
                {
                    Debug.LogError(
                        $"{name}: workplace role binding {index} has no activity ID.",
                        this);
                }
                else if (!Facility.TryGetBinding(binding.ActivityId, out _))
                {
                    Debug.LogError(
                        $"{name}: workplace activity '{binding.ActivityId}' was not found on the sibling facility.",
                        this);
                }

                if (binding.MaximumConcurrentScheduledWorkers < 1)
                {
                    Debug.LogError(
                        $"{name}: workplace role binding {index} must have capacity of at least 1.",
                        this);
                }
            }
        }
    }
}
