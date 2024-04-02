using UnityEditor;
using UnityEngine;

namespace DCL.Audio
{
    public class KeyValuePairCustomDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                label = EditorGUI.BeginProperty(position, label, property);
                label.text = label.text[^1].ToString();

                EditorGUIUtility.labelWidth = 14f;
                Rect contentPosition = EditorGUI.PrefixLabel(position, label);
                contentPosition.width *= 0.5f;
                EditorGUI.indentLevel = 0;
                EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("key"), GUIContent.none);
                contentPosition.x += contentPosition.width;
                EditorGUI.PropertyField(contentPosition, property.FindPropertyRelative("value"), GUIContent.none);
                EditorGUI.EndProperty();
            }
    }
}
