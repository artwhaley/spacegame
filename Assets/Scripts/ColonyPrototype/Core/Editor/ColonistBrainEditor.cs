using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEditor;
using UnityEngine;

namespace AsteroidColony
{
    [CustomEditor(typeof(ColonistBrain))]
    public sealed class ColonistBrainEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LIVE DEBUG", EditorStyles.boldLabel);

            ColonistBrain brain = (ColonistBrain)target;
            if (brain == null)
                return;

            GameObject owner = brain.gameObject;
            ColonistStatsComponent stats = owner.GetComponent<ColonistStatsComponent>();
            ColonistTargetResolver resolver = owner.GetComponent<ColonistTargetResolver>();
            ColonistActivityRunner runner = owner.GetComponent<ColonistActivityRunner>();
            ColonistMotor motor = owner.GetComponent<ColonistMotor>();
            SimulationManager manager = SimulationManager.Instance;
            ColonistIdentity identity = owner.GetComponent<ColonistIdentity>();
            WorkforceManager workforce = WorkforceManager.Instance;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            DrawIdentity(identity);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Employment", EditorStyles.boldLabel);
            DrawEmployment(identity, workforce);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Schedule", EditorStyles.boldLabel);
            DrawSchedule(identity, workforce, manager);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Brain", EditorStyles.boldLabel);
            ReadOnlyLabel("State", brain.State.ToString());

            EditorGUILayout.LabelField("Physiology", EditorStyles.boldLabel);
            ReadOnlyLabel("Stats", stats != null ? "OK" : "MISSING");
            if (stats != null)
            {
                ReadOnlyLabel("Fatigue", stats.Fatigue.ToString("0.##"));
                ReadOnlyLabel("Baseline Rate", FormatRate(stats.BaselineFatiguePerGameHour));
                ReadOnlyLabel("Effective Rate", FormatRate(stats.EffectiveFatiguePerGameHour));
                ReadOnlyLabel("Sleepy", stats.IsSleepy ? "YES" : "NO");
                ReadOnlyLabel("Rest Preferred", stats.ShouldPreferRest ? "YES" : "NO");
                ReadOnlyLabel("Rest Preferred Threshold", stats.RestPreferredThreshold.ToString("0.##"));
                ReadOnlyLabel("Exhausted", stats.IsExhausted ? "YES" : "NO");
                ReadOnlyLabel("Hunger", stats.Hunger.ToString("0.##"));
                ReadOnlyLabel("Baseline Hunger Rate", FormatRate(stats.BaselineHungerPerGameHour));
                ReadOnlyLabel("Effective Hunger Rate", FormatRate(stats.EffectiveHungerPerGameHour));
                ReadOnlyLabel("Hungry Threshold", stats.HungryThreshold.ToString("0.##"));
                ReadOnlyLabel("Hungry", stats.IsHungry ? "YES" : "NO");
                ReadOnlyLabel("Critical Hunger Threshold", stats.CriticalHungerThreshold.ToString("0.##"));
                ReadOnlyLabel("Critical Hunger", stats.IsCriticallyHungry ? "YES" : "NO");
                ReadOnlyLabel("Starvation Threshold", stats.StarvationThreshold.ToString("0.##"));
                ReadOnlyLabel("Starving", stats.IsStarving ? "YES" : "NO");
                ReadOnlyLabel("Stimulation Need", stats.StimulationNeed.ToString("0.##"));
                ReadOnlyLabel(
                    "Stimulation Baseline Rate",
                    FormatRate(stats.BaselineStimulationNeedPerGameHour));
                ReadOnlyLabel(
                    "Stimulation Effective Rate",
                    FormatRate(stats.EffectiveStimulationPerGameHour));
                ReadOnlyLabel(
                    "Stimulation Threshold",
                    stats.StimulationNeedThreshold.ToString("0.##"));
                ReadOnlyLabel("Needs Stimulation", stats.NeedsStimulation ? "YES" : "NO");
                ReadOnlyLabel("Relaxation Need", stats.RelaxationNeed.ToString("0.##"));
                ReadOnlyLabel(
                    "Relaxation Baseline Rate",
                    FormatRate(stats.BaselineRelaxationNeedPerGameHour));
                ReadOnlyLabel(
                    "Relaxation Effective Rate",
                    FormatRate(stats.EffectiveRelaxationPerGameHour));
                ReadOnlyLabel(
                    "Relaxation Threshold",
                    stats.RelaxationNeedThreshold.ToString("0.##"));
                ReadOnlyLabel("Needs Relaxation", stats.NeedsRelaxation ? "YES" : "NO");
                ReadOnlyLabel(
                    "Preferred OffDuty Drive",
                    FormatDrive(brain.PreferredOffDutyDrive));
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Sleep Target", EditorStyles.boldLabel);
            DrawSleepTarget(resolver);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Work Target", EditorStyles.boldLabel);
            DrawWorkTarget(resolver);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Eat Target", EditorStyles.boldLabel);
            DrawEatTarget(brain, identity, manager);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("OffDuty Planning", EditorStyles.boldLabel);
            DrawOffDutyPlanning(brain);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Leisure Cooldowns", EditorStyles.boldLabel);
            DrawCooldowns(brain, manager);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
            DrawInteraction(runner);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Decision", EditorStyles.boldLabel);
            ReadOnlyLabel("Last Decision", brain.LastDecision);
            ReadOnlyLabel("Last Decision Reason", brain.LastDecisionReason);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
            if (motor == null)
            {
                ReadOnlyLabel("Motor", "MISSING");
            }
            else
            {
                ReadOnlyLabel("Navigating", motor.IsNavigating ? "YES" : "NO");
                ReadOnlyLabel("Activity Motion", motor.IsInActivityMotion ? "YES" : "NO");
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            if (manager == null)
            {
                ReadOnlyLabel("Manager", "MISSING");
            }
            else
            {
                ReadOnlyLabel("Paused", manager.paused ? "YES" : "NO");
                ReadOnlyLabel("Speed", manager.speedMultiplier.ToString("0.##") + "x");
                ReadOnlyLabel("Presentation Factor", manager.PresentationSpeedFactor.ToString("0.##") + "x");
                ReadOnlyLabel("Game Hour", manager.CurrentGameHour.ToString("0.##"));
                ReadOnlyLabel("Tick", manager.CurrentTick.ToString());
            }

            EditorGUI.EndDisabledGroup();

            if (Application.isPlaying)
                Repaint();
        }

        private static void DrawIdentity(ColonistIdentity identity)
        {
            if (identity == null)
            {
                ReadOnlyLabel("Identity", "MISSING");
                return;
            }

            ReadOnlyLabel("Colonist", identity.DisplayName);
        }

        private static void DrawEmployment(
            ColonistIdentity identity,
            WorkforceManager workforce)
        {
            if (identity == null)
            {
                ReadOnlyLabel("Assigned", "NO");
                ReadOnlyLabel("Workplace", "NONE");
                ReadOnlyLabel("Role", "NONE");
                ReadOnlyLabel("Daily Shift", "NONE");
                return;
            }

            if (workforce == null)
            {
                ReadOnlyLabel("Assigned", "NO");
                ReadOnlyLabel("Workforce Manager", "MISSING");
                ReadOnlyLabel("Workplace", "NONE");
                ReadOnlyLabel("Role", "NONE");
                ReadOnlyLabel("Daily Shift", "NONE");
                return;
            }

            if (!workforce.TryGetAssignment(
                    identity,
                    out WorkAssignment assignment) ||
                assignment == null)
            {
                ReadOnlyLabel("Assigned", "NO");
                ReadOnlyLabel("Workplace", "NONE");
                ReadOnlyLabel("Role", "NONE");
                ReadOnlyLabel("Daily Shift", "NONE");
                return;
            }

            ReadOnlyLabel("Assigned", "YES");
            ReadOnlyLabel(
                "Validity",
                assignment.IsConfigured ? "VALID" : "INVALID");
            ReadOnlyLabel(
                "Workplace",
                assignment.Workplace != null ? assignment.Workplace.name : "MISSING");
            ReadOnlyLabel(
                "Role",
                assignment.Role != null ? assignment.Role.DisplayName : "MISSING");
            ReadOnlyLabel(
                "Daily Shift",
                assignment.Shift != null && assignment.Shift.IsConfigured
                    ? FormatShiftHours(assignment.Shift)
                    : "MISSING");
        }

        private static void DrawSchedule(
            ColonistIdentity identity,
            WorkforceManager workforce,
            SimulationManager simulation)
        {
            if (identity == null)
            {
                ReadOnlyLabel("Current Shift", "NONE");
                ReadOnlyLabel("Next Shift", "NONE");
                ReadOnlyLabel("Time Until Next", "NONE");
                return;
            }

            if (workforce == null || simulation == null)
            {
                ReadOnlyLabel("Current Shift", "UNAVAILABLE");
                ReadOnlyLabel("Next Shift", "UNAVAILABLE");
                ReadOnlyLabel("Time Until Next", "UNAVAILABLE");
                return;
            }

            float currentGameHour = simulation.CurrentGameHour;
            bool hasCurrent = workforce.TryGetCurrentShift(
                identity,
                currentGameHour,
                out ScheduledWorkOccurrence currentShift);
            ReadOnlyLabel(
                "Current Shift",
                hasCurrent ? FormatOccurrence(currentShift) : "NONE");

            bool hasNext = workforce.TryGetNextShift(
                identity,
                currentGameHour,
                out ScheduledWorkOccurrence nextShift);
            ReadOnlyLabel(
                "Next Shift",
                hasNext ? FormatOccurrence(nextShift) : "NONE");
            ReadOnlyLabel(
                "Time Until Next",
                hasNext
                    ? FormatDuration(nextShift.TimeUntilStart(currentGameHour))
                    : "NONE");
        }

        private static void DrawSleepTarget(ColonistTargetResolver resolver)
        {
            if (resolver == null)
            {
                ReadOnlyLabel("Resolver", "MISSING");
                return;
            }

            if (!resolver.TryResolveTarget(ActivityPurpose.Sleep, out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured)
            {
                ReadOnlyLabel("Resolved", "NO");
                return;
            }

            ReadOnlyLabel("Resolved", "YES");
            ReadOnlyLabel("Facility", target.Facility.name);
            ReadOnlyLabel("Activity", target.ActivityId);
        }

        private static void DrawWorkTarget(ColonistTargetResolver resolver)
        {
            if (resolver == null)
            {
                ReadOnlyLabel("Resolved", "NO");
                ReadOnlyLabel("Resolver", "MISSING");
                return;
            }

            if (!resolver.TryResolveTarget(ActivityPurpose.Work, out ActivityTarget target) ||
                target == null ||
                !target.IsConfigured)
            {
                ReadOnlyLabel("Resolved", "NO");
                return;
            }

            ReadOnlyLabel("Resolved", "YES");
            ReadOnlyLabel("Facility", target.Facility.name);
            ReadOnlyLabel("Activity", target.ActivityId);
        }

        private static void DrawInteraction(ColonistActivityRunner runner)
        {
            if (runner == null)
            {
                ReadOnlyLabel("Runner", "MISSING");
                return;
            }

            ReadOnlyLabel("Has Active Request", runner.HasActiveRequest ? "YES" : "NO");
            ReadOnlyLabel("Reservation", runner.HasActiveRequest ? "HELD" : "NONE");
            ReadOnlyLabel("Reservation Group", runner.CurrentReservationGroup ?? "NONE");
            ReadOnlyLabel("Phase", runner.Phase.ToString());
            ReadOnlyLabel("Current Activity", runner.CurrentActivityId ?? "NONE");
            ReadOnlyLabel("Active Activity", runner.ActiveActivityId ?? "NONE");
            ReadOnlyLabel("Activity Active", runner.IsActivityActive ? "YES" : "NO");
            ReadOnlyLabel(
                "Active Facility",
                runner.ActiveFacility != null ? runner.ActiveFacility.name : "NONE");
        }

        private static void DrawEatTarget(
            ColonistBrain brain,
            ColonistIdentity identity,
            SimulationManager simulation)
        {
            if (identity == null)
            {
                ReadOnlyLabel("Food Bid/Offer", "IDENTITY MISSING");
                return;
            }

            FoodManager manager = FoodManager.Instance;
            long currentTick = simulation != null ? simulation.CurrentTick : 0L;
            if (manager == null)
            {
                ReadOnlyLabel("Food Bid/Offer", "MANAGER MISSING");
                return;
            }

            ReadOnlyLabel("Food Bid", brain.LastSubmittedFoodBid != null ? "SUBMITTED" : "NONE");
            ReadOnlyLabel("Offer Chosen", brain.LastOfferChosen);
            ReadOnlyLabel("Offer Rejection", brain.LastOfferRejectionReason);

            if (manager.TryPeekOffer(identity, currentTick, out FoodOffer offer))
            {
                ReadOnlyLabel("Food Offer", "PRESENT");
                ReadOnlyLabel("Offer Valid Tick", offer.ValidForSimulationTick.ToString());
                ReadOnlyLabel(
                    "Facility",
                    offer.Opportunity != null && offer.Opportunity.Service != null
                        ? offer.Opportunity.Service.name
                        : "NONE");
                ReadOnlyLabel(
                    "Access Mode",
                    offer.Opportunity != null
                        ? FoodManager.DescribeAccessMode(offer.Opportunity.AccessMode)
                        : "UNKNOWN");
                return;
            }

            ReadOnlyLabel("Food Offer", "NONE");
            ReadOnlyLabel("Bids This Round", manager.BidsThisRound.Count.ToString());
            ReadOnlyLabel("Food Services", manager.Services.Count.ToString());
        }

        private static void DrawOffDutyPlanning(ColonistBrain brain)
        {
            ReadOnlyLabel("Next Work", brain.NextWork != null
                ? FormatOccurrence(brain.NextWork)
                : "NONE");
            ReadOnlyLabel("Time Until Work", FormatDuration(brain.TimeUntilWork));
            ReadOnlyLabel("Protected Sleep Required", FormatDuration(brain.ProtectedSleepRequired));
            ReadOnlyLabel("Maximum Discretionary Duration", FormatDuration(brain.MaximumDiscretionaryDuration));
            ReadOnlyLabel(
                "OffDuty Target",
                brain.OffDutyTarget != null && brain.OffDutyTarget.Target != null
                    ? brain.OffDutyTarget.Target.Facility.name
                    : "NONE");
            ReadOnlyLabel(
                "OffDuty Planned Duration",
                FormatDuration(brain.OffDutyPlannedDuration));
            ReadOnlyLabel(
                "OffDuty Active Duration",
                FormatDuration(brain.OffDutyActiveDuration));
            ReadOnlyLabel("Active OffDuty Drive", FormatDrive(brain.ActiveOffDutyDrive));
            ReadOnlyLabel("OffDuty Bids", brain.LastSubmittedOffDutyBids.Count.ToString());
            ReadOnlyLabel("Offer Chosen", brain.LastOfferChosen);
            ReadOnlyLabel("Offer Rejection", brain.LastOfferRejectionReason);
            SimulationManager simulation = SimulationManager.Instance;
            OffDutyManager manager = OffDutyManager.Instance;
            if (manager != null && brain != null &&
                manager.TryPeekOffer(
                    brain.GetComponent<ColonistIdentity>(),
                    simulation != null ? simulation.CurrentTick : 0L,
                    out OffDutyOffer offer))
            {
                ReadOnlyLabel("OffDuty Offer", offer.Opportunity.Target.ActivityId);
                ReadOnlyLabel("Offer Valid Tick", offer.ValidForSimulationTick.ToString());
            }
            else
            {
                ReadOnlyLabel("OffDuty Offer", "NONE");
            }
        }

        private static void DrawCooldowns(
            ColonistBrain brain,
            SimulationManager simulation)
        {
            OffDutyCompletionHistory history = brain.OffDutyCompletionHistory;
            if (history == null || history.Records.Count == 0)
            {
                ReadOnlyLabel("Cooldowns", "NONE");
                return;
            }

            float currentGameHour = simulation != null ? simulation.CurrentGameHour : 0f;
            for (int index = 0; index < history.Records.Count; index++)
            {
                OffDutyCompletionRecord record = history.Records[index];
                float cooldownHours = FindAuthoredCooldownHours(record.CooldownKey);
                float cooldownUntil =
                    record.LastCompletedAbsoluteGameHour + cooldownHours;
                ReadOnlyLabel(
                    "Cooldown " + record.CooldownKey,
                    cooldownHours > 0f && currentGameHour < cooldownUntil
                        ? FormatDuration(cooldownUntil - currentGameHour) + " remaining"
                        : "ready");
            }
        }

        private static float FindAuthoredCooldownHours(string cooldownKey)
        {
            OffDutyManager manager = OffDutyManager.Instance;
            if (manager == null || string.IsNullOrWhiteSpace(cooldownKey))
                return 0f;

            float hours = 0f;
            IReadOnlyList<OffDutyComponent> providers = manager.Providers;
            for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                OffDutyComponent provider = providers[providerIndex];
                if (provider == null)
                    continue;

                IReadOnlyList<OffDutyActivityBinding> activities = provider.Activities;
                for (int activityIndex = 0; activityIndex < activities.Count; activityIndex++)
                {
                    OffDutyActivityBinding activity = activities[activityIndex];
                    if (activity == null ||
                        !string.Equals(
                            activity.CooldownKey,
                            cooldownKey,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (activity.CooldownGameHours > hours)
                        hours = activity.CooldownGameHours;
                }
            }

            return hours;
        }

        private static string FormatDrive(OffDutyDrive? drive)
        {
            if (!drive.HasValue)
                return "NONE";

            return drive.Value == OffDutyDrive.Stimulation ? "stimulation" : "relaxation";
        }

        private static string FormatOccurrence(ScheduledWorkOccurrence occurrence)
        {
            return SimulationTime.FormatTimestamp(occurrence.StartGameHour) +
                   " → " +
                   SimulationTime.FormatTimestamp(occurrence.EndGameHour);
        }

        private static string FormatShiftHours(DailyShiftWindow shift)
        {
            return SimulationTime.FormatHourOfDay(shift.StartHour) +
                   " → " +
                   SimulationTime.FormatHourOfDay(shift.EndHour);
        }

        private static string FormatDuration(float gameHours)
        {
            if (float.IsNaN(gameHours) || float.IsInfinity(gameHours))
                return "NONE";

            int totalMinutes = Mathf.Max(0, Mathf.RoundToInt(gameHours * 60f));
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            if (hours == 0)
                return minutes + "m";
            if (minutes == 0)
                return hours + "h";
            return hours + "h " + minutes + "m";
        }

        private static string FormatRate(float rate)
        {
            return rate.ToString("+0.##;-0.##;0") + " / game-hour";
        }

        private static void ReadOnlyLabel(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }
    }
}
