using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public class RecipeStaffingRule
    {
        public int minimumWorkers;
        public int maximumEffectiveWorkers;
        public WorkerClassDefinition requiredClass;
        public SkillDefinition preferredSkill;
        public float additionalWorkerBonus;
        public float maxPreferredSkillBonus;

        public bool IsStaffed(IReadOnlyList<ColonistAgent> workers)
        {
            return CountEffectiveWorkers(workers) >= Mathf.Max(0, minimumWorkers);
        }

        public float CalculateThroughputMultiplier(IReadOnlyList<ColonistAgent> workers)
        {
            int minimum = Mathf.Max(0, minimumWorkers);
            int maximum = maximumEffectiveWorkers > 0
                ? Mathf.Max(minimum, maximumEffectiveWorkers)
                : int.MaxValue;
            int effective = Mathf.Min(CountEffectiveWorkers(workers), maximum);
            if (effective < minimum)
                return 0f;

            float multiplier = 1f + Mathf.Max(0f, effective - minimum) * Mathf.Max(0f, additionalWorkerBonus);
            if (preferredSkill != null && effective > 0)
            {
                float total = 0f;
                int counted = 0;
                for (int i = 0; i < workers.Count; i++)
                {
                    ColonistAgent worker = workers[i];
                    if (worker == null || (requiredClass != null && !worker.HasClass(requiredClass)))
                        continue;
                    total += worker.GetSkill(preferredSkill);
                    counted++;
                    if (counted >= maximum)
                        break;
                }

                if (counted > 0)
                    multiplier += total / counted * Mathf.Max(0f, maxPreferredSkillBonus);
            }
            return multiplier;
        }

        private int CountEffectiveWorkers(IReadOnlyList<ColonistAgent> workers)
        {
            if (workers == null)
                return 0;

            int count = 0;
            for (int i = 0; i < workers.Count; i++)
            {
                ColonistAgent worker = workers[i];
                if (worker != null && (minimumWorkers == 0 && requiredClass == null ||
                    requiredClass != null && worker.HasClass(requiredClass) ||
                    requiredClass == null && minimumWorkers > 0))
                    count++;
            }
            return count;
        }
    }
}
