using Colony.Interactions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnimationSegment))]
public sealed class AnimationSegmentDrawer : PropertyDrawer
{
    private static readonly string[] Names =
    {
        "clip", "speed", "blendDuration", "placementReference", "customPlacementAnchor",
        "placementPositionOffset", "placementEulerOffset", "contacts"
    };

    private static readonly string[] Labels =
    {
        "Clip", "Speed", "Blend Duration", "Placement Reference", "Custom Placement Anchor",
        "Placement Position Offset", "Placement Rotation Offset", "Contacts"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float y = position.y;
        Rect foldout = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int index = 0; index < Names.Length; index++)
            {
                SerializedProperty child = property.FindPropertyRelative(Names[index]);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                float childHeight = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, childHeight),
                    child,
                    new GUIContent(Labels[index]),
                    true);
                y += childHeight - EditorGUIUtility.singleLineHeight;
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
        for (int index = 0; index < Names.Length; index++)
        {
            height += EditorGUIUtility.standardVerticalSpacing +
                      EditorGUI.GetPropertyHeight(property.FindPropertyRelative(Names[index]), true);
        }

        return height;
    }
}
