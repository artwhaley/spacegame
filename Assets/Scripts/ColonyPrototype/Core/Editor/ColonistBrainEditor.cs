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

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Physiology", EditorStyles.boldLabel);
            ReadOnlyLabel("Stats", stats != null ? "OK" : "MISSING");
            if (stats != null)
            {
                ReadOnlyLabel("Fatigue", stats.Fatigue.ToString("0.##"));
                ReadOnlyLabel("Baseline Rate", FormatRate(stats.BaselineFatiguePerGameHour));
                ReadOnlyLabel("Effective Rate", FormatRate(stats.EffectiveFatiguePerGameHour));
                ReadOnlyLabel("Sleepy", stats.IsSleepy ? "YES" : "NO");
                ReadOnlyLabel("Exhausted", stats.IsExhausted ? "YES" : "NO");
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Brain", EditorStyles.boldLabel);
            ReadOnlyLabel("State", brain.State.ToString());

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Sleep Target", EditorStyles.boldLabel);
            DrawSleepTarget(resolver);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
            DrawInteraction(runner);

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
                ReadOnlyLabel("Target", "UNRESOLVED");
                return;
            }

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

            ReadOnlyLabel("Reservation", runner.HasActiveRequest ? "HELD" : "NONE");
            ReadOnlyLabel("Reservation Group", runner.CurrentReservationGroup ?? "NONE");
            ReadOnlyLabel("Phase", runner.Phase.ToString());
            ReadOnlyLabel("Current Activity", runner.CurrentActivityId ?? "NONE");
            ReadOnlyLabel("Active Activity", runner.ActiveActivityId ?? "NONE");
            ReadOnlyLabel("Activity Active", runner.IsActivityActive ? "YES" : "NO");
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
