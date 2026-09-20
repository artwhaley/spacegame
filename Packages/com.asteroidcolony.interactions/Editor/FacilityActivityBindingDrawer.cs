using Colony.Interactions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FacilityActivityBinding))]
public sealed class FacilityActivityBindingDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "activityId", "reservationGroup", "externallyRequestable", "completionMode",
        "approachAnchor", "animationAnchor", "exitAnchor", "targets",
        "entrySteps", "loopStep", "activeSteps", "exitSteps"
    };

    private static readonly string[] PropertyLabels =
    {
        "Activity Id", "Reservation Group", "Externally Requestable", "Completion Mode",
        "Approach Anchor", "Animation Anchor", "Exit Anchor", "Targets",
        "Entry Steps", "Loop Step", "Active Steps", "Exit Steps"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float y = position.y;
        Rect line = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        string activityId = property.FindPropertyRelative("activityId").stringValue;
        string heading = string.IsNullOrWhiteSpace(activityId) ? label.text : $"{label.text}: {activityId}";
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, heading, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int index = 0; index < PropertyNames.Length; index++)
            {
                SerializedProperty child = property.FindPropertyRelative(PropertyNames[index]);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                float height = EditorGUI.GetPropertyHeight(child, true);
                line = new Rect(position.x, y, position.width, height);
                EditorGUI.PropertyField(line, child, new GUIContent(PropertyLabels[index]), true);
                y += height - EditorGUIUtility.singleLineHeight;
            }

            y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            Rect buttonRect = new Rect(
                EditorGUI.IndentedRect(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight)));
            if (GUI.Button(buttonRect, "Create Missing Anchors & Targets"))
            {
                CreateMissingTransforms(property);
            }

            y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            buttonRect = new Rect(
                EditorGUI.IndentedRect(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight)));
            if (GUI.Button(buttonRect, "Open Activity Placement Preview"))
            {
                OpenPlacementPreview(property);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float height = EditorGUIUtility.singleLineHeight;
        for (int index = 0; index < PropertyNames.Length; index++)
        {
            SerializedProperty child = property.FindPropertyRelative(PropertyNames[index]);
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
        }

        height += 2f * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        return height;
    }

    private static void CreateMissingTransforms(SerializedProperty property)
    {
        InteractableFacility facility = property.serializedObject.targetObject as InteractableFacility;
        if (facility == null || !HasMissingTransform(property))
        {
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Facility Activity Anchors");
        string activityId = property.FindPropertyRelative("activityId").stringValue;
        string safeActivityId = string.IsNullOrWhiteSpace(activityId) ? "Activity" : activityId.Trim();
        string slotName = $"ActivitySlot_{safeActivityId}";
        Transform slot = FindAssignedSlot(property, facility.transform, slotName) ?? facility.transform.Find(slotName);
        if (slot == null)
        {
            slot = CreateChild(facility.transform, slotName, facility.gameObject.layer);
        }

        AssignMissingTransform(property, "approachAnchor", "ApproachAnchor", slot);
        AssignMissingTransform(property, "animationAnchor", "AnimationAnchor", slot);
        AssignMissingTransform(property, "exitAnchor", "ExitAnchor", slot);
        AssignMissingTransform(property, "targets", "Targets", slot);
        property.serializedObject.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(facility);
        EditorUtility.SetDirty(facility);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void OpenPlacementPreview(SerializedProperty property)
    {
        InteractableFacility facility = property.serializedObject.targetObject as InteractableFacility;
        if (facility != null)
        {
            ActivityPlacementPreviewWindow.Open(
                facility,
                property.FindPropertyRelative("activityId").stringValue);
        }
    }

    private static bool HasMissingTransform(SerializedProperty binding)
    {
        return binding.FindPropertyRelative("approachAnchor").objectReferenceValue == null ||
               binding.FindPropertyRelative("animationAnchor").objectReferenceValue == null ||
               binding.FindPropertyRelative("exitAnchor").objectReferenceValue == null ||
               binding.FindPropertyRelative("targets").objectReferenceValue == null;
    }

    private static Transform FindAssignedSlot(SerializedProperty binding, Transform facilityRoot, string slotName)
    {
        string[] names = { "approachAnchor", "animationAnchor", "exitAnchor", "targets" };
        for (int index = 0; index < names.Length; index++)
        {
            Transform assigned = binding.FindPropertyRelative(names[index]).objectReferenceValue as Transform;
            if (assigned != null && assigned.parent != null &&
                assigned.parent.name == slotName && assigned.parent.IsChildOf(facilityRoot))
            {
                return assigned.parent;
            }
        }

        return null;
    }

    private static void AssignMissingTransform(
        SerializedProperty binding,
        string propertyName,
        string childName,
        Transform slot)
    {
        SerializedProperty transformProperty = binding.FindPropertyRelative(propertyName);
        if (transformProperty.objectReferenceValue != null)
        {
            return;
        }

        Transform child = slot.Find(childName) ?? CreateChild(slot, childName, slot.gameObject.layer);
        transformProperty.objectReferenceValue = child;
    }

    private static Transform CreateChild(Transform parent, string name, int layer)
    {
        GameObject childObject = new GameObject(name) { layer = layer };
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {name}");
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }
}
