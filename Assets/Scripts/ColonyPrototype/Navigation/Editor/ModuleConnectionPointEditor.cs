using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(ModuleConnectionPoint))]
    public sealed class ModuleConnectionPointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ModuleConnectionPoint point = (ModuleConnectionPoint)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Connection", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Current Partner", point.CurrentPartner, typeof(ModuleConnectionPoint), true);
                EditorGUILayout.ObjectField("Current Link", point.CurrentLink, typeof(Component), true);
                EditorGUILayout.Toggle("Connected", point.IsConnected);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Discover And Connect All Points"))
            {
                ModuleConnectionPoint.DiscoverAndConnect();
                EditorUtility.SetDirty(point);
            }

            if (GUILayout.Button("Disconnect This Point"))
            {
                point.Disconnect();
                EditorUtility.SetDirty(point);
            }
        }
    }
}
