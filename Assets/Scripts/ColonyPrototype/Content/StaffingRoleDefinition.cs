using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// One effect channel a role publishes. The contribution depends only on the
    /// number of active qualified workers, never on which worker came first, so
    /// there are no ordered worker slots.
    /// </summary>
    [Serializable]
    public class StaffingEffectRule
    {
        public FacilityEffectDefinition effect;
        public List<float> multiplierByActiveCount = new List<float> { 1f };
        public SkillDefinition bonusSkill;
        public float maxSkillBonusFraction;

        /// <summary>Curve value at an active-worker count, clamped to the final defined index.</summary>
        public float CurveAt(int activeCount)
        {
            if (multiplierByActiveCount == null || multiplierByActiveCount.Count == 0)
                return 1f;

            int index = Mathf.Max(0, activeCount);
            if (index >= multiplierByActiveCount.Count)
                index = multiplierByActiveCount.Count - 1;
            return Mathf.Max(0f, multiplierByActiveCount[index]);
        }

        /// <summary>Base curve value modified by the average bonus skill of the supplied active workers.</summary>
        public float Evaluate(IReadOnlyList<ColonistAgent> activeWorkers)
        {
            int count = activeWorkers != null ? activeWorkers.Count : 0;
            float value = CurveAt(count);
            if (bonusSkill == null || maxSkillBonusFraction <= 0f || count == 0)
                return value;

            float total = 0f;
            for (int i = 0; i < activeWorkers.Count; i++)
                if (activeWorkers[i] != null)
                    total += activeWorkers[i].GetSkill(bonusSkill);
            return value * (1f + total / count * maxSkillBonusFraction);
        }

        public bool Validate(out string error)
        {
            if (effect == null)
            {
                error = "Effect rule requires a facility effect.";
                return false;
            }

            if (multiplierByActiveCount == null || multiplierByActiveCount.Count == 0)
            {
                error = $"Effect rule for '{effect.displayName}' requires at least one multiplier (index 0).";
                return false;
            }

            for (int i = 0; i < multiplierByActiveCount.Count; i++)
            {
                float value = multiplierByActiveCount[i];
                if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                {
                    error = $"Effect rule for '{effect.displayName}' has an invalid multiplier at index {i}.";
                    return false;
                }
            }

            if (maxSkillBonusFraction < 0f || float.IsNaN(maxSkillBonusFraction) || float.IsInfinity(maxSkillBonusFraction))
            {
                error = $"Effect rule for '{effect.displayName}' has an invalid skill bonus fraction.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// What a workplace wants a class of worker to do. A role declares the class
    /// that is eligible, an operational gate, a per-shift capacity, and the effect
    /// curves its active workers publish.
    /// </summary>
    [CreateAssetMenu(menuName = "Asteroid Colony/Staffing Role Definition", fileName = "StaffingRoleDefinition")]
    public class StaffingRoleDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        [TextArea]
        public string description;

        public WorkerClassDefinition requiredClass;
        public int minimumActiveForOperation = 1;
        public int maximumAssignedPerShift = 3;
        [Min(0f)] public float exertionMultiplier = 1f;
        public List<StaffingEffectRule> effects = new List<StaffingEffectRule>();

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Staffing role stableId is required.";
                return false;
            }

            if (minimumActiveForOperation < 0)
            {
                error = "minimumActiveForOperation must be at least zero.";
                return false;
            }

            if (maximumAssignedPerShift < 1)
            {
                error = "maximumAssignedPerShift must be at least one.";
                return false;
            }

            if (minimumActiveForOperation > maximumAssignedPerShift)
            {
                error = "minimumActiveForOperation cannot exceed maximumAssignedPerShift.";
                return false;
            }

            if (requiredClass == null)
            {
                error = "requiredClass is required for every staffing role.";
                return false;
            }

            if (exertionMultiplier < 0f || float.IsNaN(exertionMultiplier) || float.IsInfinity(exertionMultiplier))
            {
                error = "exertionMultiplier must be finite and non-negative.";
                return false;
            }

            if (effects == null)
            {
                error = string.Empty;
                return true;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                StaffingEffectRule rule = effects[i];
                if (rule == null)
                {
                    error = $"Effect rule {i} is null.";
                    return false;
                }
                if (!rule.Validate(out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
            minimumActiveForOperation = Mathf.Max(0, minimumActiveForOperation);
            maximumAssignedPerShift = Mathf.Max(1, maximumAssignedPerShift);
            exertionMultiplier = float.IsNaN(exertionMultiplier) || float.IsInfinity(exertionMultiplier)
                ? 1f
                : Mathf.Max(0f, exertionMultiplier);
            if (!Validate(out string error))
                Debug.LogError($"Invalid staffing role {name}: {error}", this);
        }
    }
}
