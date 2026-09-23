using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(ShuttleRcsSmokeController))]
    internal sealed class ShuttleRcsSmokeControllerEditor : UnityEditor.Editor
    {
        private ShuttleRcsVfxProfile displayedProfile;
        private SerializedObject profileProperties;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            ShuttleRcsSmokeController controller = (ShuttleRcsSmokeController)target;
            ShuttleRcsVfxProfile profile = controller.VfxProfile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign an RCS VFX Profile.", MessageType.Warning);
                return;
            }
            if (displayedProfile != profile || profileProperties == null)
            {
                displayedProfile = profile;
                profileProperties = new SerializedObject(profile);
            }
            profileProperties.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Exhaust jet appearance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Presentation only. Edit during Play for live feedback; save the VFX profile to retain values.", MessageType.Info);
            DrawField("puffSize", "Jet Width");
            DrawField("puffSizeVariation", "Width Variation");
            DrawField("puffLifetime", "Particle Lifetime");
            DrawField("exhaustSpeed", "Exit Speed");
            DrawField("particlesPerBurst", "Particles Per Pulse");
            DrawField("spreadDegrees", "Cone Half Angle");
            DrawField("puffOpacity", "Opacity");
            DrawField("minimumDirectionAlignment", "Nozzle Alignment");
            DrawField("nozzleExitOffset", "Nozzle Exit Offset");
            profileProperties.ApplyModifiedProperties();
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Authored Jets", controller.NozzleCount.ToString());
                if (GUILayout.Button("Preview Burst (All Jets)"))
                    controller.PreviewBurst();
            }
            if (GUILayout.Button("Save VFX Profile"))
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawField(string name, string label)
        {
            SerializedProperty property = profileProperties.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }
}
