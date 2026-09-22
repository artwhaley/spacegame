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

        [SerializeField]
        private ColonistIdentity identity;

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

        // Soft discretionary drives. They are not death meters: zero means fully satisfied
        // and a larger value means a stronger unmet discretionary drive. Nothing in Stack 1
        // attaches a health consequence to them.
        [SerializeField, Min(0f)]
        private float stimulationNeed;

        [SerializeField]
        private float baselineStimulationNeedPerGameHour = 4f;

        [SerializeField, Min(0f)]
        private float stimulationNeedThreshold = 50f;

        [SerializeField, Min(0f)]
        private float relaxationNeed;

        [SerializeField]
        private float baselineRelaxationNeedPerGameHour = 4f;

        [SerializeField, Min(0f)]
        private float relaxationNeedThreshold = 50f;

        private bool thresholdStateInitialized;
        private bool wasHungry;
        private bool wasCriticallyHungry;
        private bool wasSleepy;
        private bool wasRestPreferred;
        private bool wasStimulationNeeded;
        private bool wasRelaxationNeeded;
        private InteractableFacility cachedOffDutyFacility;
        private OffDutyComponent cachedOffDutyProvider;

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

        // Effective leisure rates are baseline accumulation minus the recovery authored by the
        // discretionary activity the colonist is genuinely performing right now. Navigation,
        // entry, exit and released state all receive the plain baseline rate.
        public float EffectiveStimulationPerGameHour =>
            BaselineStimulationNeedPerGameHour - ActiveOffDutyRecovery(true);

        public float EffectiveRelaxationPerGameHour =>
            BaselineRelaxationNeedPerGameHour - ActiveOffDutyRecovery(false);

        public float SleepyThreshold => sleepyThreshold;

        public float RestPreferredThreshold => restPreferredThreshold;

        public float ExhaustionThreshold => exhaustionThreshold;

        public float HungryThreshold => hungryThreshold;

        public float StarvationThreshold => starvationThreshold;

        public float CriticalHungerThreshold => criticalHungerThreshold;

        public float StimulationNeed => stimulationNeed;

        public float BaselineStimulationNeedPerGameHour =>
            IsFinite(baselineStimulationNeedPerGameHour)
                ? Mathf.Max(0f, baselineStimulationNeedPerGameHour)
                : 0f;

        public float StimulationNeedThreshold => stimulationNeedThreshold;

        public bool NeedsStimulation => stimulationNeed >= stimulationNeedThreshold;

        public float RelaxationNeed => relaxationNeed;

        public float BaselineRelaxationNeedPerGameHour =>
            IsFinite(baselineRelaxationNeedPerGameHour)
                ? Mathf.Max(0f, baselineRelaxationNeedPerGameHour)
                : 0f;

        public float RelaxationNeedThreshold => relaxationNeedThreshold;

        public bool NeedsRelaxation => relaxationNeed >= relaxationNeedThreshold;

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

            if (identity == null)
                identity = GetComponent<ColonistIdentity>();

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

            float stimulationChange = EffectiveStimulationPerGameHour * deltaGameHours;
            if (IsFinite(stimulationChange))
                ApplyStimulationDelta(stimulationChange);

            float relaxationChange = EffectiveRelaxationPerGameHour * deltaGameHours;
            if (IsFinite(relaxationChange))
                ApplyRelaxationDelta(relaxationChange);
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

        public void AdjustStimulationNeed(float amount)
        {
            if (!IsFinite(amount))
                return;

            ApplyStimulationDelta(amount);
        }

        public void AdjustRelaxationNeed(float amount)
        {
            if (!IsFinite(amount))
                return;

            ApplyRelaxationDelta(amount);
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

            if (!IsFinite(stimulationNeed))
                stimulationNeed = 0f;
            stimulationNeed = Mathf.Max(0f, stimulationNeed);

            if (!IsFinite(baselineStimulationNeedPerGameHour))
                baselineStimulationNeedPerGameHour = 0f;

            if (!IsFinite(stimulationNeedThreshold))
                stimulationNeedThreshold = 0f;
            stimulationNeedThreshold = Mathf.Max(0f, stimulationNeedThreshold);

            if (!IsFinite(relaxationNeed))
                relaxationNeed = 0f;
            relaxationNeed = Mathf.Max(0f, relaxationNeed);

            if (!IsFinite(baselineRelaxationNeedPerGameHour))
                baselineRelaxationNeedPerGameHour = 0f;

            if (!IsFinite(relaxationNeedThreshold))
                relaxationNeedThreshold = 0f;
            relaxationNeedThreshold = Mathf.Max(0f, relaxationNeedThreshold);
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

        private void ApplyStimulationDelta(float amount)
        {
            if (!IsFinite(stimulationNeed))
                stimulationNeed = 0f;

            float updatedNeed = stimulationNeed + amount;
            if (!IsFinite(updatedNeed))
                return;

            stimulationNeed = Mathf.Max(0f, updatedNeed);
            RecordThresholdTransitions();
        }

        private void ApplyRelaxationDelta(float amount)
        {
            if (!IsFinite(relaxationNeed))
                relaxationNeed = 0f;

            float updatedNeed = relaxationNeed + amount;
            if (!IsFinite(updatedNeed))
                return;

            relaxationNeed = Mathf.Max(0f, updatedNeed);
            RecordThresholdTransitions();
        }

        private float ActiveOffDutyRecovery(bool stimulation)
        {
            OffDutyActivityBinding activity = ResolveActiveOffDutyActivity();
            if (activity == null)
                return 0f;

            return stimulation
                ? activity.StimulationRecoveryPerGameHour
                : activity.RelaxationRecoveryPerGameHour;
        }

        private OffDutyActivityBinding ResolveActiveOffDutyActivity()
        {
            if (activityRunner == null || !activityRunner.IsActivityActive)
                return null;

            InteractableFacility facility = activityRunner.ActiveFacility;
            string activityId = activityRunner.ActiveActivityId;
            if (facility == null || string.IsNullOrWhiteSpace(activityId))
                return null;

            OffDutyComponent provider;
            if (cachedOffDutyFacility == facility)
            {
                provider = cachedOffDutyProvider;
            }
            else
            {
                provider = facility.GetComponent<OffDutyComponent>();
                cachedOffDutyFacility = facility;
                cachedOffDutyProvider = provider;
            }

            if (provider == null ||
                !provider.TryGetActivity(activityId, out OffDutyActivityBinding activity) ||
                !provider.IsLive(activity))
            {
                return null;
            }

            return activity;
        }

        private void CaptureThresholdState()
        {
            wasHungry = IsHungry;
            wasCriticallyHungry = IsCriticallyHungry;
            wasSleepy = IsSleepy;
            wasRestPreferred = ShouldPreferRest;
            wasStimulationNeeded = NeedsStimulation;
            wasRelaxationNeeded = NeedsRelaxation;
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
            bool stimulationNeeded = NeedsStimulation;
            bool relaxationNeeded = NeedsRelaxation;

            RecordTransition(
                "colonist.need.hungry",
                wasHungry,
                hungry,
                Hunger,
                HungryThreshold);
            RecordTransition(
                "colonist.need.critical_hunger",
                wasCriticallyHungry,
                criticallyHungry,
                Hunger,
                CriticalHungerThreshold);
            RecordTransition(
                "colonist.need.sleepy",
                wasSleepy,
                sleepy,
                Fatigue,
                SleepyThreshold);
            RecordTransition(
                "colonist.need.rest_preferred",
                wasRestPreferred,
                restPreferred,
                Fatigue,
                RestPreferredThreshold);
            RecordTransition(
                "colonist.need.stimulation",
                wasStimulationNeeded,
                stimulationNeeded,
                StimulationNeed,
                StimulationNeedThreshold);
            RecordTransition(
                "colonist.need.relaxation",
                wasRelaxationNeeded,
                relaxationNeeded,
                RelaxationNeed,
                RelaxationNeedThreshold);

            wasHungry = hungry;
            wasCriticallyHungry = criticallyHungry;
            wasSleepy = sleepy;
            wasRestPreferred = restPreferred;
            wasStimulationNeeded = stimulationNeeded;
            wasRelaxationNeeded = relaxationNeeded;
        }

        /// <summary>
        /// Canonical structured-log subject. A need transition belongs to the colonist, not to
        /// this stats component, so a query by ColonistIdentity finds threshold history even
        /// though several of the colonist's components record events.
        /// </summary>
        private UnityEngine.Object LogSubject
        {
            get
            {
                if (identity == null)
                    identity = GetComponent<ColonistIdentity>();

                return identity != null ? (UnityEngine.Object)identity : (UnityEngine.Object)this;
            }
        }

        private void RecordTransition(
            string eventKey,
            bool wasActive,
            bool isActive,
            float value,
            float threshold)
        {
            if (wasActive == isActive)
                return;

            SimulationLogManager.RecordEvent(
                eventKey,
                "Need",
                "Info",
                LogSubject,
                null,
                new SimulationLogField("state", isActive ? "entered" : "exited"),
                new SimulationLogField("value", value),
                new SimulationLogField("threshold", threshold));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
