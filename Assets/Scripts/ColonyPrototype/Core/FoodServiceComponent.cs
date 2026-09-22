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

        [Header("Physical Food Accounting")]
        [SerializeField]
        private bool inventoryAccountingEnabled;

        [SerializeField]
        private InventoryComponent foodInventory;

        [SerializeField]
        private ResourceDefinition foodResource;

        [SerializeField, Min(0.0001f)]
        private float foodPerMeal = 1f;

        [NonSerialized] private bool staticBindingCacheInitialized;
        [NonSerialized] private InteractableFacility cachedFacility;
        [NonSerialized] private string cachedEatActivityId;
        [NonSerialized] private FacilityActivityBinding cachedEatBinding;
        [NonSerialized] private bool cachedRequiredStaffConfiguration;
        [NonSerialized] private bool cachedRequiresStaff;
        [NonSerialized] private WorkplaceComponent cachedRequiredWorkplace;
        [NonSerialized] private JobRoleDefinition cachedRequiredRole;
        [NonSerialized] private int cachedMinimumActiveWorkers;

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

        /// <summary>When enabled, discovery and meal acceptance use real inventory stock.</summary>
        public bool InventoryAccountingEnabled => inventoryAccountingEnabled;
        public InventoryComponent FoodInventory => foodInventory;
        public ResourceDefinition FoodResource => foodResource;
        public float FoodPerMeal =>
            IsFinite(foodPerMeal) ? Mathf.Max(0f, foodPerMeal) : 0f;

        public bool HasUsableFoodInventory =>
            !inventoryAccountingEnabled ||
            (foodInventory != null && foodResource != null && FoodPerMeal > 0f);

        public float FoodOnHand =>
            HasUsableFoodInventory && foodInventory != null && foodResource != null
                ? foodInventory.GetOnHand(foodResource)
                : 0f;

        public float FoodReserved =>
            HasUsableFoodInventory && foodInventory != null && foodResource != null
                ? foodInventory.GetReserved(foodResource)
                : 0f;

        public float FoodAvailable =>
            HasUsableFoodInventory && foodInventory != null && foodResource != null
                ? foodInventory.GetAvailable(foodResource)
                : 0f;

        public bool HasAvailableMeal()
        {
            return !inventoryAccountingEnabled ||
                   (HasUsableFoodInventory && FoodAvailable + 0.0001f >= FoodPerMeal);
        }

        /// <summary>
        /// Structural authoring validity only. This deliberately does NOT include whether a
        /// worker happens to be standing here right now: an already-eating colonist must not
        /// lose Hunger recovery merely because the waiter clocked out.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                EnsureStaticBindingCache();
                if (!isActiveAndEnabled ||
                    cachedFacility == null ||
                    string.IsNullOrWhiteSpace(eatActivityId) ||
                    HungerRecoveryPerGameHour <= 0f ||
                    cachedEatBinding == null ||
                    !cachedRequiredStaffConfiguration ||
                    !IsUsableBinding(cachedEatBinding) ||
                    (inventoryAccountingEnabled && !HasUsableFoodInventory))
                {
                    return false;
                }

                return true;
            }
        }

        private static bool IsUsableBinding(FacilityActivityBinding binding)
        {
            return binding != null &&
                   binding.ExternallyRequestable &&
                   !string.IsNullOrWhiteSpace(binding.ReservationGroup) &&
                   binding.ApproachAnchor != null;
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

            return EvaluateAccessFromCachedConfiguration(requester, absoluteGameHour);
        }

        /// <summary>
        /// Dynamic access evaluation for a candidate that already passed the cached static
        /// binding check. This intentionally avoids another IsConfigured/facility lookup.
        /// </summary>
        public FoodServiceAccessMode EvaluateAccessFromCachedConfiguration(
            ColonistIdentity requester,
            float absoluteGameHour)
        {
            EnsureStaticBindingCache();
            if (cachedEatBinding == null ||
                !cachedRequiredStaffConfiguration ||
                !IsUsableBinding(cachedEatBinding) ||
                (inventoryAccountingEnabled && !HasUsableFoodInventory))
            {
                return FoodServiceAccessMode.Unavailable;
            }

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
            RefreshStaticBindingMetadata();
        }

        private void OnEnable()
        {
            ResolveFacility();
            RefreshStaticBindingMetadata();
            if (FoodManager.Instance != null)
                FoodManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            staticBindingCacheInitialized = false;
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
            RefreshStaticBindingMetadata();
        }

        public bool TryGetEatBinding(out FacilityActivityBinding binding)
        {
            EnsureStaticBindingCache();
            binding = cachedEatBinding;
            return binding != null;
        }

        /// <summary>
        /// Returns the cached structural result without re-scanning the facility activity list.
        /// Dynamic staffing, reservation and inventory facts are intentionally not included.
        /// </summary>
        public bool TryGetCachedEatBinding(out FacilityActivityBinding binding)
        {
            EnsureStaticBindingCache();
            binding = cachedEatBinding;
            return isActiveAndEnabled &&
                   HungerRecoveryPerGameHour > 0f &&
                   cachedEatBinding != null &&
                   cachedRequiredStaffConfiguration &&
                   IsUsableBinding(cachedEatBinding);
        }

        public void RefreshStaticBindingMetadata()
        {
            ResolveFacility();
            cachedFacility = facility;
            cachedEatActivityId = eatActivityId;
            cachedEatBinding = null;
            if (cachedFacility != null && !string.IsNullOrWhiteSpace(cachedEatActivityId))
                cachedFacility.TryGetBinding(cachedEatActivityId, out cachedEatBinding);

            cachedRequiredWorkplace = requiredWorkplace;
            cachedRequiredRole = requiredRole;
            cachedMinimumActiveWorkers = minimumActiveWorkers;
            cachedRequiresStaff = requiresStaff;
            cachedRequiredStaffConfiguration = !requiresStaff ||
                (cachedRequiredWorkplace != null &&
                 cachedRequiredRole != null &&
                 cachedMinimumActiveWorkers >= 1 &&
                 cachedRequiredWorkplace.Facility != null &&
                 cachedRequiredWorkplace.TryGetRoleBinding(cachedRequiredRole, out _));
            staticBindingCacheInitialized = true;
        }

        private void EnsureStaticBindingCache()
        {
            ResolveFacility();
            if (!staticBindingCacheInitialized ||
                cachedFacility != facility ||
                !string.Equals(cachedEatActivityId, eatActivityId, StringComparison.Ordinal) ||
                cachedRequiresStaff != requiresStaff ||
                cachedRequiredWorkplace != requiredWorkplace ||
                cachedRequiredRole != requiredRole ||
                cachedMinimumActiveWorkers != minimumActiveWorkers)
            {
                RefreshStaticBindingMetadata();
            }
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
