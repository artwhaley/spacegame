using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public class ColonistStatsComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        [SerializeField, Min(0f)]
        private float fatigue;

        [SerializeField]
        private float baselineFatiguePerGameHour = 5f;

        [SerializeField, Min(0f)]
        private float sleepyThreshold = 70f;

        [SerializeField, Min(0f)]
        private float exhaustionThreshold = 100f;

        private bool hasFatigueRateOverride;
        private float fatigueRateOverride;

        public float Fatigue => fatigue;

        public float BaselineFatiguePerGameHour => baselineFatiguePerGameHour;

        public float EffectiveFatiguePerGameHour =>
            hasFatigueRateOverride ? fatigueRateOverride : baselineFatiguePerGameHour;

        public float SleepyThreshold => sleepyThreshold;

        public float ExhaustionThreshold => exhaustionThreshold;

        public bool IsSleepy => fatigue >= sleepyThreshold;

        public bool IsExhausted => fatigue >= exhaustionThreshold;

        public bool HasFatigueRateOverride => hasFatigueRateOverride;

        public int SimulationTickPriority => 50;

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
            if (!IsFinite(fatigueChange))
                return;

            ApplyFatigueDelta(fatigueChange);
        }

        public void AdjustFatigue(float amount)
        {
            if (!IsFinite(amount))
                return;

            ApplyFatigueDelta(amount);
        }

        public void SetFatigueRateOverride(float fatiguePerGameHour)
        {
            if (!IsFinite(fatiguePerGameHour))
                return;

            fatigueRateOverride = fatiguePerGameHour;
            hasFatigueRateOverride = true;
        }

        public void ClearFatigueRateOverride()
        {
            hasFatigueRateOverride = false;
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

            if (hasFatigueRateOverride && !IsFinite(fatigueRateOverride))
                hasFatigueRateOverride = false;
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
