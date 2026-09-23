using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(ShuttleVoyageComponent))]
    internal sealed class ShuttleVoyageComponentEditor : UnityEditor.Editor
    {
        private ShuttleFlightProfile displayedProfile;
        private SerializedObject profileProperties;
        private ShuttleNavigationProfile displayedNavigationProfile;
        private SerializedObject navigationProperties;

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
                EditorGUILayout.LabelField("Route", serializedObject.FindProperty("plannedRouteKind").enumDisplayNames[
                    serializedObject.FindProperty("plannedRouteKind").enumValueIndex]);
                EditorGUILayout.LabelField("Route Result", serializedObject.FindProperty("routeDiagnostic").stringValue);
            }
            if (GUILayout.Button("Save Flight Profile"))
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            ShuttleNavigationProfile navigation = voyage.NavigationProfile;
            if (navigation != null)
            {
                if (displayedNavigationProfile != navigation || navigationProperties == null)
                {
                    displayedNavigationProfile = navigation;
                    navigationProperties = new SerializedObject(navigation);
                }
                navigationProperties.Update();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Strategic navigation", EditorStyles.boldLabel);
                DrawNavigationField("navigationRadius", "Shuttle Physical Radius");
                DrawNavigationField("preferredClearance", "Preferred Clearance");
                DrawNavigationField("emergencyClearance", "Emergency Clearance");
                DrawNavigationField("detourPadding", "Detour Padding");
                DrawNavigationField("maxRelevantObstacles", "Obstacle Budget");
                DrawNavigationField("maxExpansionRounds", "Expansion Rounds");
                DrawNavigationField("maxCandidatesPerObstacle", "Candidates Per Obstacle");
                DrawNavigationField("maxGraphNodes", "Graph Node Budget");
                DrawNavigationField("maxEdgeTests", "Edge Sweep Budget");
                DrawNavigationField("straightTurnDegrees", "Straight Turn Threshold");
                DrawNavigationField("moderateTurnDegrees", "Moderate Turn Threshold");
                DrawNavigationField("sharpTurnDegrees", "Sharp Turn Threshold");
                DrawNavigationField("moderateSpeedMultiplier", "Moderate Turn Speed");
                DrawNavigationField("sharpSpeedMultiplier", "Sharp Turn Speed");
                DrawNavigationField("hairpinSpeedMultiplier", "Hairpin Speed");
                DrawNavigationField("cornerLookaheadDistance", "Corner Braking Lookahead");
                DrawNavigationField("replanCooldownSeconds", "Safety Replan Cooldown");
                EditorGUILayout.LabelField("Effective sweep radius", navigation.SweptRadius.ToString("0.###"));
                navigationProperties.ApplyModifiedProperties();
                if (GUILayout.Button("Save Navigation Profile"))
                {
                    EditorUtility.SetDirty(navigation);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private void DrawField(string name, string label)
        {
            SerializedProperty property = profileProperties.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void DrawNavigationField(string name, string label)
        {
            SerializedProperty property = navigationProperties.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }
}
