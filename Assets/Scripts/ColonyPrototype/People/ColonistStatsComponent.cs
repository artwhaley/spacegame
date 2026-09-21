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

        [SerializeField, Min(0f)]
        private float preferredWorkStartFatigue = 30f;

        [SerializeField]
        private ColonistActivityRunner activityRunner;

        [SerializeField, Min(0f)]
        private float sleepyThreshold = 70f;

        [SerializeField, Min(0f)]
        private float restPreferredThreshold = 60f;

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

        [SerializeField, Min(0f)]
        private float criticalHungerThreshold = 90f;

        private bool thresholdStateInitialized;
        private bool wasHungry;
        private bool wasCriticallyHungry;
        private bool wasSleepy;
        private bool wasRestPreferred;

        public float Fatigue => fatigue;

        public float Hunger => hunger;

        public float BaselineHungerPerGameHour => baselineHungerPerGameHour;

        public float BaselineFatiguePerGameHour => baselineFatiguePerGameHour;

        public float PreferredWorkStartFatigue => preferredWorkStartFatigue;

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

        public float RestPreferredThreshold => restPreferredThreshold;

        public float ExhaustionThreshold => exhaustionThreshold;

        public float HungryThreshold => hungryThreshold;

        public float StarvationThreshold => starvationThreshold;

        public float CriticalHungerThreshold => criticalHungerThreshold;

        public bool IsSleepy => fatigue >= sleepyThreshold;

        public bool ShouldPreferRest => fatigue >= restPreferredThreshold;

        public bool IsExhausted => fatigue >= exhaustionThreshold;

        public bool IsHungry => hunger >= hungryThreshold;

        public bool IsCriticallyHungry => hunger >= criticalHungerThreshold;

        public bool IsStarving => hunger >= starvationThreshold;

        public int SimulationTickPriority => 50;

        private void Awake()
        {
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();

            CaptureThresholdState();
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

            if (!IsFinite(preferredWorkStartFatigue))
                preferredWorkStartFatigue = 0f;
            preferredWorkStartFatigue = Mathf.Max(0f, preferredWorkStartFatigue);

            if (!IsFinite(sleepyThreshold))
                sleepyThreshold = 0f;
            sleepyThreshold = Mathf.Max(0f, sleepyThreshold);

            if (!IsFinite(restPreferredThreshold))
                restPreferredThreshold = 0f;
            restPreferredThreshold = Mathf.Clamp(restPreferredThreshold, 0f, sleepyThreshold);

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

            if (!IsFinite(criticalHungerThreshold))
                criticalHungerThreshold = hungryThreshold;
            criticalHungerThreshold = Mathf.Clamp(
                criticalHungerThreshold,
                hungryThreshold,
                starvationThreshold);

        }

        private void ApplyFatigueDelta(float amount)
        {
            if (!IsFinite(fatigue))
                fatigue = 0f;

            float updatedFatigue = fatigue + amount;
            if (!IsFinite(updatedFatigue))
                return;

            fatigue = Mathf.Max(0f, updatedFatigue);
            RecordThresholdTransitions();
        }

        private void ApplyHungerDelta(float amount)
        {
            if (!IsFinite(hunger))
                hunger = 0f;

            float updatedHunger = hunger + amount;
            if (!IsFinite(updatedHunger))
                return;

            hunger = Mathf.Max(0f, updatedHunger);
            RecordThresholdTransitions();
        }

        private void CaptureThresholdState()
        {
            wasHungry = IsHungry;
            wasCriticallyHungry = IsCriticallyHungry;
            wasSleepy = IsSleepy;
            wasRestPreferred = ShouldPreferRest;
            thresholdStateInitialized = true;
        }

        private void RecordThresholdTransitions()
        {
            if (!thresholdStateInitialized)
            {
                CaptureThresholdState();
                return;
            }

            bool hungry = IsHungry;
            bool criticallyHungry = IsCriticallyHungry;
            bool sleepy = IsSleepy;
            bool restPreferred = ShouldPreferRest;

            RecordTransition(
                "colonist.need.hungry",
                wasHungry,
                hungry,
                HungryThreshold);
            RecordTransition(
                "colonist.need.critical_hunger",
                wasCriticallyHungry,
                criticallyHungry,
                CriticalHungerThreshold);
            RecordTransition(
                "colonist.need.sleepy",
                wasSleepy,
                sleepy,
                SleepyThreshold);
            RecordTransition(
                "colonist.need.rest_preferred",
                wasRestPreferred,
                restPreferred,
                RestPreferredThreshold);

            wasHungry = hungry;
            wasCriticallyHungry = criticallyHungry;
            wasSleepy = sleepy;
            wasRestPreferred = restPreferred;
        }

        private void RecordTransition(
            string eventKey,
            bool wasActive,
            bool isActive,
            float threshold)
        {
            if (wasActive == isActive)
                return;

            SimulationLogManager.RecordEvent(
                eventKey,
                "Need",
                "Info",
                this,
                null,
                new SimulationLogField("state", isActive ? "entered" : "exited"),
                new SimulationLogField("value", eventKey.Contains("hunger") ? Hunger : Fatigue),
                new SimulationLogField("threshold", threshold));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
