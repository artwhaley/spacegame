using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public enum FoodSelfServicePolicy
    {
        None,
        AssignedWorkers,
        Everyone
    }

    public enum FoodServiceAccessMode
    {
        Unavailable,
        Public,
        PublicStaffed,
        SelfService
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableFacility))]
    public sealed class FoodServiceComponent : MonoBehaviour
    {
        [SerializeField]
        private InteractableFacility facility;

        [SerializeField]
        private string eatActivityId = "Eat";

        [SerializeField, Min(0f)]
        private float hungerRecoveryPerGameHour = 60f;

        [SerializeField]
        private bool requiresStaff;

        [SerializeField]
        private WorkplaceComponent requiredWorkplace;

        [SerializeField]
        private JobRoleDefinition requiredRole;

        [SerializeField, Min(1)]
        private int minimumActiveWorkers = 1;

        [SerializeField]
        private FoodSelfServicePolicy selfServicePolicy = FoodSelfServicePolicy.None;

        public InteractableFacility Facility
        {
            get
            {
                ResolveFacility();
                return facility;
            }
        }

        public string EatActivityId => eatActivityId;

        public float HungerRecoveryPerGameHour =>
            IsFinite(hungerRecoveryPerGameHour)
                ? Mathf.Max(0f, hungerRecoveryPerGameHour)
                : 0f;

        public bool RequiresStaff => requiresStaff;

        public WorkplaceComponent RequiredWorkplace => requiredWorkplace;

        public JobRoleDefinition RequiredRole => requiredRole;

        public int MinimumActiveWorkers => Mathf.Max(1, minimumActiveWorkers);

        public FoodSelfServicePolicy SelfServicePolicy => selfServicePolicy;

        /// <summary>
        /// Structural authoring validity only. This deliberately does NOT include whether a
        /// worker happens to be standing here right now: an already-eating colonist must not
        /// lose Hunger recovery merely because the waiter clocked out.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                if (!isActiveAndEnabled ||
                    Facility == null ||
                    string.IsNullOrWhiteSpace(eatActivityId) ||
                    HungerRecoveryPerGameHour <= 0f ||
                    !TryGetEatBinding(out FacilityActivityBinding binding) ||
                    !binding.ExternallyRequestable ||
                    string.IsNullOrWhiteSpace(binding.ReservationGroup) ||
                    binding.ApproachAnchor == null)
                {
                    return false;
                }

                if (!requiresStaff)
                    return true;

                if (requiredWorkplace == null ||
                    requiredRole == null ||
                    minimumActiveWorkers < 1 ||
                    requiredWorkplace.Facility == null ||
                    !requiredWorkplace.TryGetRoleBinding(requiredRole, out _))
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Answers "can this specific requester use this service right now?" Structural
        /// configuration, public service availability and requester-specific access are three
        /// different questions and are kept separate here.
        /// </summary>
        public FoodServiceAccessMode EvaluateAccess(
            ColonistIdentity requester,
            float absoluteGameHour)
        {
            if (!IsConfigured)
                return FoodServiceAccessMode.Unavailable;

            if (!requiresStaff)
                return FoodServiceAccessMode.Public;

            if (HasActivePublicStaff(absoluteGameHour))
                return FoodServiceAccessMode.PublicStaffed;

            return AllowsSelfService(requester)
                ? FoodServiceAccessMode.SelfService
                : FoodServiceAccessMode.Unavailable;
        }

        /// <summary>
        /// Whether public service is currently staffed by a physically Working worker.
        /// Food only requires worker presence to serve or start the meal, so no minimum
        /// remaining shift duration is demanded here.
        /// </summary>
        public bool HasActivePublicStaff(float absoluteGameHour)
        {
            if (!requiresStaff ||
                requiredWorkplace == null ||
                requiredRole == null ||
                WorkforceManager.Instance == null)
            {
                return !requiresStaff;
            }

            float gameHour = IsFinite(absoluteGameHour) ? absoluteGameHour : 0f;
            return WorkforceManager.Instance.HasEnoughActiveWorkers(
                requiredWorkplace,
                requiredRole,
                MinimumActiveWorkers,
                0f,
                gameHour);
        }

        /// <summary>
        /// Requester-specific access that survives being off shift. An assigned Cafeteria
        /// Worker may make themselves a sandwich even while the public counter is closed;
        /// that permission comes from their assignment, not from them currently working.
        /// </summary>
        public bool AllowsSelfService(ColonistIdentity requester)
        {
            if (!requiresStaff)
                return false;

            switch (selfServicePolicy)
            {
                case FoodSelfServicePolicy.Everyone:
                    return true;

                case FoodSelfServicePolicy.AssignedWorkers:
                    if (requester == null || WorkforceManager.Instance == null)
                        return false;

                    if (!WorkforceManager.Instance.TryGetAssignment(
                            requester,
                            out WorkAssignment assignment) ||
                        assignment == null ||
                        assignment.Workplace == null ||
                        assignment.Role == null)
                    {
                        return false;
                    }

                    return assignment.Workplace == requiredWorkplace &&
                           assignment.Role == requiredRole;

                default:
                    return false;
            }
        }

        private void Awake()
        {
            ResolveFacility();
        }

        private void OnEnable()
        {
            ResolveFacility();
            if (FoodManager.Instance != null)
                FoodManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (FoodManager.Instance != null)
                FoodManager.Instance.Unregister(this);
        }

        private void OnValidate()
        {
            ResolveFacility();
            if (!IsFinite(hungerRecoveryPerGameHour))
                hungerRecoveryPerGameHour = 0f;
            hungerRecoveryPerGameHour = Mathf.Max(0f, hungerRecoveryPerGameHour);
            minimumActiveWorkers = Mathf.Max(1, minimumActiveWorkers);
        }

        public bool TryGetEatBinding(out FacilityActivityBinding binding)
        {
            ResolveFacility();
            if (facility != null &&
                !string.IsNullOrWhiteSpace(eatActivityId) &&
                facility.TryGetBinding(eatActivityId, out binding))
            {
                return binding != null;
            }

            binding = null;
            return false;
        }

        private void ResolveFacility()
        {
            if (facility == null)
                facility = GetComponent<InteractableFacility>();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
