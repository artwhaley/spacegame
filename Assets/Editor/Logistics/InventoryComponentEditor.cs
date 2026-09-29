using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(InventoryComponent))]
    public sealed class InventoryComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            InventoryComponent inventory = (InventoryComponent)target;
            if (inventory == null)
                return;

            if (Application.isPlaying)
                Repaint();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shared Physical Capacity", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Capacity", inventory.Capacity);
                EditorGUILayout.FloatField("Used", inventory.UsedCapacity);
                EditorGUILayout.FloatField("Free", inventory.FreeCapacity);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Resource", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Total", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                EditorGUILayout.LabelField("Reserved", EditorStyles.miniBoldLabel, GUILayout.Width(68f));
                EditorGUILayout.LabelField("Available", EditorStyles.miniBoldLabel, GUILayout.Width(68f));
                EditorGUILayout.EndHorizontal();

                IReadOnlyList<InventoryEntry> entries = inventory.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    InventoryEntry entry = entries[i];
                    if (entry == null || entry.resource == null || entry.onHand <= 0f && entry.reserved <= 0f)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.resource.displayName);
                    EditorGUILayout.LabelField(entry.onHand.ToString("0.##"), GUILayout.Width(58f));
                    EditorGUILayout.LabelField(entry.reserved.ToString("0.##"), GUILayout.Width(68f));
                    EditorGUILayout.LabelField(entry.Available.ToString("0.##"), GUILayout.Width(68f));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
