using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    public sealed class ColonistFreeTimePlan
    {
        public float MaximumSafeDiscretionaryDuration { get; internal set; }
        public float RequiredProtectedSleepDuration { get; internal set; }
        public float TimeUntilWork { get; internal set; }
        public float ProjectedFatigueAtWork { get; internal set; }
        public string Reason { get; internal set; }
        public bool HasUpcomingWork { get; internal set; }

        public bool HasBudget => MaximumSafeDiscretionaryDuration > 0f;
    }

    public static class ColonistFreeTimePlanner
    {
        public const float WorkPreparationLeadGameHours = 0.5f;

        public static ColonistFreeTimePlan Calculate(
            ColonistStatsComponent stats,
            float currentGameHour)
        {
            if (stats == null)
                return EmptyPlan("missing_stats");

            ColonistIdentity identity = stats.GetComponent<ColonistIdentity>();
            ColonistAssignments assignments = stats.GetComponent<ColonistAssignments>();
            return Calculate(stats, identity, assignments, currentGameHour);
        }

        public static ColonistFreeTimePlan Calculate(
            ColonistStatsComponent stats,
            ColonistIdentity identity,
            ColonistAssignments assignments,
            float currentGameHour)
        {
            ColonistFreeTimePlan plan = EmptyPlan("no_upcoming_work");
            if (stats == null || float.IsNaN(currentGameHour) || float.IsInfinity(currentGameHour))
            {
                plan.Reason = "invalid_planning_input";
                return plan;
            }

            WorkforceManager workforce = WorkforceManager.Instance;
            ScheduledWorkOccurrence nextShift = null;
            if (workforce != null &&
                workforce.TryGetCurrentShift(identity, currentGameHour, out _))
            {
                plan.HasUpcomingWork = true;
                plan.TimeUntilWork = 0f;
                plan.Reason = "current_work";
                return plan;
            }

            bool hasUpcomingWork = workforce != null &&
                workforce.TryGetNextShift(identity, currentGameHour, out nextShift);
            plan.HasUpcomingWork = hasUpcomingWork;
            plan.TimeUntilWork = hasUpcomingWork
                ? nextShift.TimeUntilStart(currentGameHour)
                : float.PositiveInfinity;

            float fatigueHeadroom = TimeUntilThreshold(
                stats.Fatigue,
                stats.RestPreferredThreshold,
                stats.BaselineFatiguePerGameHour);
            float hungerHeadroom = TimeUntilThreshold(
                stats.Hunger,
                stats.HungryThreshold,
                stats.BaselineHungerPerGameHour);

            if (stats.IsHungry)
            {
                plan.Reason = "hungry_threshold";
                return plan;
            }

            if (fatigueHeadroom <= 0f)
            {
                plan.Reason = "rest_preferred_threshold";
                return plan;
            }

            float maximum = Mathf.Min(fatigueHeadroom, hungerHeadroom);
            if (hasUpcomingWork)
            {
                if (plan.TimeUntilWork <= WorkPreparationLeadGameHours)
                {
                    plan.Reason = "work_preparation_window";
                    return plan;
                }

                if (!TryGetSleepRecovery(assignments, out float sleepRecoveryPerGameHour))
                {
                    plan.Reason = "missing_sleep_recovery";
                    return plan;
                }

                float availableBeforeWork =
                    plan.TimeUntilWork - WorkPreparationLeadGameHours;
                maximum = Mathf.Min(
                    maximum,
                    MaximumDurationThatProtectsWorkRest(
                        stats,
                        availableBeforeWork,
                        sleepRecoveryPerGameHour,
                        out float requiredSleep,
                        out float projectedFatigue));
                plan.RequiredProtectedSleepDuration = requiredSleep;
                plan.ProjectedFatigueAtWork = projectedFatigue;
                if (maximum <= 0f)
                {
                    plan.Reason = "insufficient_rest_budget";
                    return plan;
                }
            }

            if (maximum <= 0f)
            {
                plan.Reason = hungerHeadroom <= fatigueHeadroom
                    ? "hungry_headroom"
                    : "rest_headroom";
                return plan;
            }

            plan.MaximumSafeDiscretionaryDuration = maximum;
            plan.Reason = "safe_discretionary_budget";
            return plan;
        }

        private static float MaximumDurationThatProtectsWorkRest(
            ColonistStatsComponent stats,
            float availableBeforeWork,
            float sleepRecoveryPerGameHour,
            out float requiredSleep,
            out float projectedFatigue)
        {
            float baselineFatigueRate = Mathf.Max(0f, stats.BaselineFatiguePerGameHour);
            float fatigueAtZeroActivity = stats.Fatigue;
            float preferred = stats.PreferredWorkStartFatigue;

            float maximum;
            if (fatigueAtZeroActivity >= preferred)
            {
                maximum = 0f;
            }
            else if (baselineFatigueRate <= 0f)
            {
                maximum = availableBeforeWork;
            }
            else
            {
                float timeToPreferred = (preferred - fatigueAtZeroActivity) / baselineFatigueRate;
                if (timeToPreferred >= availableBeforeWork)
                {
                    maximum = availableBeforeWork;
                }
                else
                {
                    maximum =
                        (availableBeforeWork * sleepRecoveryPerGameHour +
                         preferred - fatigueAtZeroActivity) /
                        (sleepRecoveryPerGameHour + baselineFatigueRate);
                }
            }

            maximum = Mathf.Max(0f, maximum);
            projectedFatigue = fatigueAtZeroActivity + baselineFatigueRate * maximum;
            requiredSleep = Mathf.Max(
                0f,
                (projectedFatigue - preferred) / sleepRecoveryPerGameHour);
            return maximum;
        }

        private static bool TryGetSleepRecovery(
            ColonistAssignments assignments,
            out float recoveryPerGameHour)
        {
            recoveryPerGameHour = 0f;
            if (assignments == null ||
                !assignments.TryGetSleepTarget(out ActivityTarget target) ||
                target == null ||
                !target.Facility.TryGetBinding(target.ActivityId, out FacilityActivityBinding binding) ||
                !binding.OverridesFatigueRate ||
                float.IsNaN(binding.FatiguePerGameHour) ||
                float.IsInfinity(binding.FatiguePerGameHour) ||
                binding.FatiguePerGameHour >= 0f)
            {
                return false;
            }

            recoveryPerGameHour = -binding.FatiguePerGameHour;
            return recoveryPerGameHour > 0f;
        }

        private static float TimeUntilThreshold(
            float current,
            float threshold,
            float rate)
        {
            if (current >= threshold)
                return 0f;
            if (rate <= 0f || float.IsNaN(rate) || float.IsInfinity(rate))
                return float.PositiveInfinity;
            return (threshold - current) / rate;
        }

        private static ColonistFreeTimePlan EmptyPlan(string reason)
        {
            return new ColonistFreeTimePlan
            {
                MaximumSafeDiscretionaryDuration = 0f,
                RequiredProtectedSleepDuration = 0f,
                TimeUntilWork = float.PositiveInfinity,
                ProjectedFatigueAtWork = 0f,
                Reason = reason,
                HasUpcomingWork = false
            };
        }
    }
}
