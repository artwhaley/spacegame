using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AsteroidColony
{
    [CustomEditor(typeof(SimulationLogManager))]
    public sealed class SimulationLogManagerEditor : Editor
    {
        private Vector2 scroll;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            SimulationLogManager manager = (SimulationLogManager)target;
            if (manager == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Structured Simulation History", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("JSONL", string.IsNullOrEmpty(manager.JsonlPath)
                ? "Not started"
                : manager.JsonlPath);

            IReadOnlyList<SimulationLogEntry> entries = manager.Entries;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No structured events yet.", MessageType.Info);
            }
            else
            {
                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    SimulationLogEntry entry = entries[index];
                    if (entry != null)
                        EditorGUILayout.LabelField(entry.Render());
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
