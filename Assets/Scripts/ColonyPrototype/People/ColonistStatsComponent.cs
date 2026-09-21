using UnityEngine;
using Colony.Interactions;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class ColonistStatsComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        [SerializeField, Min(0f)]
        private float fatigue;

        [SerializeField]
        private float baselineFatiguePerGameHour = 5f;

        [SerializeField]
        private ColonistActivityRunner activityRunner;

        [SerializeField, Min(0f)]
        private float sleepyThreshold = 70f;

        [SerializeField, Min(0f)]
        private float exhaustionThreshold = 100f;

        [SerializeField, Min(0f)]
        private float hunger;

        [SerializeField]
        private float baselineHungerPerGameHour = 8f;

        [SerializeField, Min(0f)]
        private float hungryThreshold = 60f;

        [SerializeField, Min(0f)]
        private float starvationThreshold = 100f;

        public float Fatigue => fatigue;

        public float Hunger => hunger;

        public float BaselineHungerPerGameHour => baselineHungerPerGameHour;

        public float BaselineFatiguePerGameHour => baselineFatiguePerGameHour;

        public float EffectiveFatiguePerGameHour
        {
            get
            {
                FacilityActivityBinding activity =
                    activityRunner != null
                        ? activityRunner.ActiveActivityBinding
                        : null;

                if (activity != null &&
                    activity.OverridesFatigueRate &&
                    IsFinite(activity.FatiguePerGameHour))
                {
                    return activity.FatiguePerGameHour;
                }

                return baselineFatiguePerGameHour;
            }
        }

        public float EffectiveHungerPerGameHour
        {
            get
            {
                if (activityRunner != null &&
                    activityRunner.ActiveFacility != null &&
                    string.Equals(
                        activityRunner.ActiveActivityId,
                        "Eat",
                        System.StringComparison.Ordinal))
                {
                    FoodServiceComponent service =
                        activityRunner.ActiveFacility.GetComponent<FoodServiceComponent>();
                    if (service != null &&
                        service.IsConfigured &&
                        string.Equals(
                            service.EatActivityId,
                            activityRunner.ActiveActivityId,
                            System.StringComparison.Ordinal))
                    {
                        return -service.HungerRecoveryPerGameHour;
                    }
                }

                return baselineHungerPerGameHour;
            }
        }

        public float SleepyThreshold => sleepyThreshold;

        public float ExhaustionThreshold => exhaustionThreshold;

        public float HungryThreshold => hungryThreshold;

        public float StarvationThreshold => starvationThreshold;

        public bool IsSleepy => fatigue >= sleepyThreshold;

        public bool IsExhausted => fatigue >= exhaustionThreshold;

        public bool IsHungry => hunger >= hungryThreshold;

        public bool IsStarving => hunger >= starvationThreshold;

        public int SimulationTickPriority => 50;

        private void Awake()
        {
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (deltaGameHours <= 0f || !IsFinite(deltaGameHours))
                return;

            float fatigueChange = EffectiveFatiguePerGameHour * deltaGameHours;
            if (IsFinite(fatigueChange))
                ApplyFatigueDelta(fatigueChange);

            float hungerChange = EffectiveHungerPerGameHour * deltaGameHours;
            if (IsFinite(hungerChange))
                ApplyHungerDelta(hungerChange);
        }

        public void AdjustFatigue(float amount)
        {
            if (!IsFinite(amount))
                return;

            ApplyFatigueDelta(amount);
        }

        public void AdjustHunger(float amount)
        {
            if (!IsFinite(amount))
                return;

            ApplyHungerDelta(amount);
        }

        private void OnValidate()
        {
            if (!IsFinite(fatigue))
                fatigue = 0f;
            fatigue = Mathf.Max(0f, fatigue);

            if (!IsFinite(baselineFatiguePerGameHour))
                baselineFatiguePerGameHour = 0f;

            if (!IsFinite(sleepyThreshold))
                sleepyThreshold = 0f;
            sleepyThreshold = Mathf.Max(0f, sleepyThreshold);

            if (!IsFinite(exhaustionThreshold))
                exhaustionThreshold = 0f;
            exhaustionThreshold = Mathf.Max(0f, exhaustionThreshold);

            if (!IsFinite(hunger))
                hunger = 0f;
            hunger = Mathf.Max(0f, hunger);

            if (!IsFinite(baselineHungerPerGameHour))
                baselineHungerPerGameHour = 0f;

            if (!IsFinite(hungryThreshold))
                hungryThreshold = 0f;
            hungryThreshold = Mathf.Max(0f, hungryThreshold);

            if (!IsFinite(starvationThreshold))
                starvationThreshold = 0f;
            starvationThreshold = Mathf.Max(0f, starvationThreshold);

        }

        private void ApplyFatigueDelta(float amount)
        {
            if (!IsFinite(fatigue))
                fatigue = 0f;

            float updatedFatigue = fatigue + amount;
            if (!IsFinite(updatedFatigue))
                return;

            fatigue = Mathf.Max(0f, updatedFatigue);
        }

        private void ApplyHungerDelta(float amount)
        {
            if (!IsFinite(hunger))
                hunger = 0f;

            float updatedHunger = hunger + amount;
            if (!IsFinite(updatedHunger))
                return;

            hunger = Mathf.Max(0f, updatedHunger);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
