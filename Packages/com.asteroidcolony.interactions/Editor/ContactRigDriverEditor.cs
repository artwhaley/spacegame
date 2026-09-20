using Colony.Interactions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContactRigDriver))]
public sealed class ContactRigDriverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Map semantic channels such as RightHand to this character's own IK constraint and proxy target. Facility activities supply the contact transform.",
            MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Reset Contact Rig Weights"))
        {
            foreach (Object targetObject in targets)
            {
                ContactRigDriver driver = targetObject as ContactRigDriver;
                if (driver == null)
                {
                    continue;
                }

                Undo.RecordObject(driver, "Reset Contact Rig Weights");
                driver.ResetRigWeights();
                EditorUtility.SetDirty(driver);
            }
        }
    }
}
