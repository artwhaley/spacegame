using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class FoodManager : MonoBehaviour
    {
        public static FoodManager Instance { get; private set; }

        [SerializeField]
        private List<FoodServiceComponent> services = new List<FoodServiceComponent>();

        private string lastSelectionSignature;

        public IReadOnlyList<FoodServiceComponent> Services =>
            services ?? (IReadOnlyList<FoodServiceComponent>)Array.Empty<FoodServiceComponent>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active FoodManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            RegisterExistingServices();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(FoodServiceComponent service)
        {
            if (service == null)
                return;

            PruneServices();
            if (!services.Contains(service))
                services.Add(service);
        }

        public void Unregister(FoodServiceComponent service)
        {
            if (service == null || services == null)
                return;

            services.Remove(service);
        }

        public bool TryFindFoodTarget(
            Vector3 seekerPosition,
            out ActivityTarget target)
        {
            if (TryFindFoodService(seekerPosition, out FoodServiceComponent service))
            {
                target = new ActivityTarget(service.Facility, service.EatActivityId);
                RecordSelection(service);
                return target.IsConfigured;
            }

            target = null;
            if (!string.Equals(lastSelectionSignature, "none", StringComparison.Ordinal))
            {
                lastSelectionSignature = "none";
                SimulationLogManager.RecordEvent(
                    "food.no_target",
                    "Food",
                    "Info",
                    this,
                    null,
                    new SimulationLogField("reason", "no_valid_food_service"));
            }
            return false;
        }

        public bool TryFindFoodService(
            Vector3 seekerPosition,
            out FoodServiceComponent service)
        {
            service = null;
            PruneServices();

            float bestDistance = float.PositiveInfinity;
            FoodServiceComponent bestService = null;
            for (int index = 0; index < services.Count; index++)
            {
                FoodServiceComponent candidate = services[index];
                if (!IsDiscoverable(candidate))
                    continue;

                FacilityActivityBinding binding = null;
                candidate.TryGetEatBinding(out binding);
                float distance =
                    (binding.ApproachAnchor.position - seekerPosition).sqrMagnitude;
                if (distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     IsPreferredTieBreak(candidate, bestService)))
                {
                    bestDistance = distance;
                    bestService = candidate;
                    service = candidate;
                }
            }

            return service != null;
        }

        public bool TryGetFoodService(
            ActivityTarget target,
            out FoodServiceComponent service)
        {
            service = null;
            if (target == null || !target.IsConfigured)
                return false;

            PruneServices();
            for (int index = 0; index < services.Count; index++)
            {
                FoodServiceComponent candidate = services[index];
                if (candidate != null &&
                    candidate.Facility == target.Facility &&
                    string.Equals(
                        candidate.EatActivityId,
                        target.ActivityId,
                        StringComparison.Ordinal) &&
                    IsConfiguredService(candidate))
                {
                    service = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsFoodTargetLive(ActivityTarget target)
        {
            return TryGetFoodService(target, out _);
        }

        private bool IsDiscoverable(FoodServiceComponent service)
        {
            if (!IsConfiguredService(service))
                return false;

            if (!service.TryGetEatBinding(out FacilityActivityBinding binding))
                return false;

            return !service.Facility.IsReserved(binding.ReservationGroup);
        }

        private bool IsConfiguredService(FoodServiceComponent service)
        {
            return service != null && service.IsConfigured;
        }

        private void RegisterExistingServices()
        {
            FoodServiceComponent[] existing = FindObjectsByType<FoodServiceComponent>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < existing.Length; index++)
                Register(existing[index]);
        }

        private static bool IsPreferredTieBreak(
            FoodServiceComponent candidate,
            FoodServiceComponent current)
        {
            if (current == null)
                return true;

            int nameComparison = string.CompareOrdinal(candidate.name, current.name);
            return nameComparison < 0;
        }

        private void RecordSelection(FoodServiceComponent service)
        {
            string signature = service != null ? service.name : "none";
            if (string.Equals(lastSelectionSignature, signature, StringComparison.Ordinal))
                return;

            lastSelectionSignature = signature;
            SimulationLogManager.RecordEvent(
                "food.target_selected",
                "Food",
                "Info",
                this,
                service != null ? service.Facility : null,
                new SimulationLogField("activityId", service != null ? service.EatActivityId : string.Empty),
                new SimulationLogField("reason", "nearest_available_service"));
        }

        private void PruneServices()
        {
            if (services == null)
                services = new List<FoodServiceComponent>();

            for (int index = services.Count - 1; index >= 0; index--)
            {
                FoodServiceComponent service = services[index];
                if (service == null)
                    services.RemoveAt(index);
            }
        }
    }
}
