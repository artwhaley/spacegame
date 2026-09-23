using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(ShuttleVoyageComponent))]
    internal sealed class ShuttleVoyageComponentEditor : UnityEditor.Editor
    {
        private ShuttleFlightProfile displayedProfile;
        private SerializedObject profileProperties;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            ShuttleVoyageComponent voyage = (ShuttleVoyageComponent)target;
            ShuttleFlightProfile profile = voyage.Profile;
            if (profile == null)
                return;
            if (displayedProfile != profile || profileProperties == null)
            {
                displayedProfile = profile;
                profileProperties = new SerializedObject(profile);
            }
            profileProperties.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flight authority", EditorStyles.boldLabel);
            DrawField("mainAcceleration", "Main Acceleration");
            DrawField("rcsAcceleration", "RCS Linear Acceleration");
            DrawField("maxCruiseSpeed", "Maximum Cruise Speed");
            DrawField("approachMaxSpeed", "Approach Speed Limit");
            DrawField("finalDockMaxSpeed", "Final Dock Speed Limit");
            DrawField("angularAcceleration", "RCS Angular Acceleration");
            DrawField("maxAngularSpeed", "Maximum Angular Speed");
            DrawField("mainBurnAlignmentDegrees", "Main Burn Alignment");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RCS flight impulses", EditorStyles.boldLabel);
            DrawField("rcsLinearPulseSeconds", "Linear Pulse Duration");
            DrawField("rcsAngularPulseSeconds", "Angular Pulse Duration");
            DrawField("rcsMinimumCoastSeconds", "Minimum Coast");
            DrawField("rcsMaxCorrectionSpeed", "Maximum RCS Travel Speed");
            DrawField("rcsMaxTurnSpeed", "Maximum RCS Turn Speed");
            DrawField("rcsPositionDeadband", "Position Deadband");
            DrawField("rcsVelocityDeadband", "Velocity Deadband");
            DrawField("rcsAngleDeadband", "Angle Deadband");
            DrawField("rcsAngularSpeedDeadband", "Angular Speed Deadband");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Guidance and capture", EditorStyles.boldLabel);
            DrawField("positionTolerance", "Position Tolerance");
            DrawField("velocityTolerance", "Velocity Tolerance");
            DrawField("angleTolerance", "Angle Tolerance");
            DrawField("captureAngularSpeedTolerance", "Capture Angular Speed");
            DrawField("integrationSubstepSeconds", "Integration Substep");
            DrawField("brakingSafetyMargin", "Braking Safety Margin");
            profileProperties.ApplyModifiedProperties();
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Linear RCS", voyage.LinearRcsActivity.ToString());
                EditorGUILayout.LabelField("Angular RCS", voyage.AngularRcsActivity.ToString());
            }
            if (GUILayout.Button("Save Flight Profile"))
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
