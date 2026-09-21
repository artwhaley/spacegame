using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
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

        public bool IsConfigured
        {
            get
            {
                return isActiveAndEnabled &&
                       Facility != null &&
                       !string.IsNullOrWhiteSpace(eatActivityId) &&
                       HungerRecoveryPerGameHour > 0f &&
                       TryGetEatBinding(out FacilityActivityBinding binding) &&
                       binding.ExternallyRequestable &&
                       !string.IsNullOrWhiteSpace(binding.ReservationGroup) &&
                       binding.ApproachAnchor != null;
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
