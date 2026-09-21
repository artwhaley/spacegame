using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
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
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableFacility))]
    public sealed class WorkplaceComponent : MonoBehaviour
    {
        [SerializeField]
        private InteractableFacility facility;

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
            return binding.Role != null &&
                   !string.IsNullOrWhiteSpace(binding.ActivityId) &&
                   binding.MaximumConcurrentScheduledWorkers >= 1 &&
                   Facility != null &&
                   Facility.TryGetBinding(binding.ActivityId, out _);
        }

        private void ResolveFacility()
        {
            if (facility == null)
                facility = GetComponent<InteractableFacility>();
        }

        private void ValidateAuthoring()
        {
            if (Facility == null)
            {
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
