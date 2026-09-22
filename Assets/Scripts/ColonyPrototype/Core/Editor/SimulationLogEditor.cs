using UnityEditor;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Inspector viewer for SimulationLog: shows a prominent, scrollable,
    /// newest-first readout of recent simulation events above the defaults.
    /// </summary>
    [CustomEditor(typeof(SimulationLog))]
    public class SimulationLogEditor : UnityEditor.Editor
    {
        private Vector2 scroll;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            SimulationLog log = (SimulationLog)target;
            if (log == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recent Simulation Events", EditorStyles.boldLabel);

            var entries = log.Entries;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No events yet. Press Play to start the simulation.", MessageType.Info);
            }
            else
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                    EditorGUILayout.LabelField(entries[i]);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
