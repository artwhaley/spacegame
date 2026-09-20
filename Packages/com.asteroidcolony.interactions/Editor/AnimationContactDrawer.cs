using Colony.Interactions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnimationContact))]
public sealed class AnimationContactDrawer : PropertyDrawer
{
    private static readonly string[] Names =
    {
        "channel", "target", "maximumWeight", "positionWeight", "rotationWeight",
        "timingMode", "startNormalizedTime", "fullWeightNormalizedTime",
        "releaseStartNormalizedTime", "endNormalizedTime", "blendInSeconds", "blendOutSeconds"
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
                if (property.FindPropertyRelative("timingMode").enumValueIndex ==
                    (int)ContactTimingMode.HoldForSegment && index >= 6 && index <= 9)
                {
                    continue;
                }

                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                float childHeight = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, childHeight), child, true);
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
        bool isHold = property.FindPropertyRelative("timingMode").enumValueIndex ==
                      (int)ContactTimingMode.HoldForSegment;
        for (int index = 0; index < Names.Length; index++)
        {
            if (isHold && index >= 6 && index <= 9)
            {
                continue;
            }

            height += EditorGUIUtility.standardVerticalSpacing +
                      EditorGUI.GetPropertyHeight(property.FindPropertyRelative(Names[index]), true);
        }

        return height;
    }
}
