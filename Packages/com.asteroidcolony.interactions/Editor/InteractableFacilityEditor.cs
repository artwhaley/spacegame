using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(Colony.Interactions.InteractableFacility))]
[CanEditMultipleObjects]
public sealed class InteractableFacilityEditor : UnityEditor.Editor
{
    private SerializedProperty activitiesProperty;
    private ReorderableList activitiesList;
    private SerializedProperty sequencesProperty;
    private ReorderableList sequencesList;

    private void OnEnable()
    {
        activitiesProperty = serializedObject.FindProperty("activities");
        activitiesList = new ReorderableList(
            serializedObject,
            activitiesProperty,
            true,
            true,
            true,
            true);

        activitiesList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Activities");

        activitiesList.elementHeightCallback = index =>
            EditorGUI.GetPropertyHeight(
                activitiesProperty.GetArrayElementAtIndex(index),
                true) + EditorGUIUtility.standardVerticalSpacing;

        activitiesList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += EditorGUIUtility.standardVerticalSpacing * 0.5f;
            EditorGUI.PropertyField(
                rect,
                activitiesProperty.GetArrayElementAtIndex(index),
                new GUIContent($"Activity {index}"),
                true);
        };

        activitiesList.onAddCallback = AddEmptyActivity;

        sequencesProperty = serializedObject.FindProperty("sequences");
        sequencesList = new ReorderableList(
            serializedObject,
            sequencesProperty,
            true,
            true,
            true,
            true);

        sequencesList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Sequences");

        sequencesList.elementHeightCallback = index =>
            EditorGUI.GetPropertyHeight(
                sequencesProperty.GetArrayElementAtIndex(index),
                true) + EditorGUIUtility.standardVerticalSpacing;

        sequencesList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += EditorGUIUtility.standardVerticalSpacing * 0.5f;
            EditorGUI.PropertyField(
                rect,
                sequencesProperty.GetArrayElementAtIndex(index),
                new GUIContent($"Sequence {index}"),
                true);
        };

        sequencesList.onAddCallback = AddEmptySequence;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        activitiesList.DoLayoutList();
        sequencesList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    private void AddEmptyActivity(ReorderableList list)
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object targetObject in targets)
        {
            Colony.Interactions.InteractableFacility facility =
                (Colony.Interactions.InteractableFacility)targetObject;
            Undo.RecordObject(facility, "Add Empty Facility Activity");
            facility.AddEmptyActivityBinding();
            EditorUtility.SetDirty(facility);
        }

        serializedObject.Update();
        list.index = activitiesProperty.arraySize - 1;
    }

    private void AddEmptySequence(ReorderableList list)
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object targetObject in targets)
        {
            Colony.Interactions.InteractableFacility facility =
                (Colony.Interactions.InteractableFacility)targetObject;
            Undo.RecordObject(facility, "Add Empty Facility Sequence");
            facility.AddEmptySequenceBinding();
            EditorUtility.SetDirty(facility);
        }

        serializedObject.Update();
        list.index = sequencesProperty.arraySize - 1;
    }
}
