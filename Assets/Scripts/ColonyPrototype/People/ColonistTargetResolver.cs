using UnityEngine;

namespace AsteroidColony
{
    public enum ActivityPurpose
    {
        Sleep,
        Work,
        Eat
    }

    [DisallowMultipleComponent]
    public sealed class ColonistTargetResolver : MonoBehaviour
    {
        [SerializeField]
        private ColonistAssignments assignments;

        [SerializeField]
        private ColonistIdentity identity;

        private void Awake()
        {
            if (assignments == null)
                assignments = GetComponent<ColonistAssignments>();

            if (identity == null)
                identity = GetComponent<ColonistIdentity>();
        }

        public bool TryResolveTarget(
            ActivityPurpose purpose,
            out ActivityTarget target)
        {
            if (TryResolvePersonalAssignment(purpose, out target))
                return true;

            if (purpose == ActivityPurpose.Work &&
                TryResolveRegularWorkAssignment(out target))
            {
                return true;
            }

            if (purpose == ActivityPurpose.Eat &&
                FoodManager.Instance != null &&
                FoodManager.Instance.TryFindFoodTarget(
                    transform.position,
                    out target))
            {
                return target != null && target.IsConfigured;
            }

            target = null;
            return false;
        }

        private bool TryResolveRegularWorkAssignment(out ActivityTarget target)
        {
            target = null;
            if (identity == null)
                identity = GetComponent<ColonistIdentity>();

            if (identity == null || WorkforceManager.Instance == null)
                return false;

            if (!WorkforceManager.Instance.TryGetAssignment(
                    identity,
                    out WorkAssignment assignment) ||
                assignment == null ||
                !assignment.IsConfigured ||
                assignment.Workplace == null ||
                assignment.Role == null)
            {
                return false;
            }

            if (!assignment.Workplace.TryGetRoleBinding(
                    assignment.Role,
                    out WorkplaceRoleBinding binding) ||
                assignment.Workplace.Facility == null ||
                string.IsNullOrWhiteSpace(binding.ActivityId) ||
                !assignment.Workplace.Facility.TryGetBinding(
                    binding.ActivityId,
                    out _))
            {
                return false;
            }

            target = new ActivityTarget(
                assignment.Workplace.Facility,
                binding.ActivityId);
            return target.IsConfigured;
        }

        private bool TryResolvePersonalAssignment(
            ActivityPurpose purpose,
            out ActivityTarget target)
        {
            target = null;

            if (assignments == null)
                assignments = GetComponent<ColonistAssignments>();

            if (assignments == null)
                return false;

            switch (purpose)
            {
                case ActivityPurpose.Sleep:
                    return assignments.TryGetSleepTarget(out target);

                default:
                    return false;
            }
        }
    }
}
