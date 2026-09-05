using UnityEditor;
using UnityEngine;

namespace Utility.Editor
{
    [CustomPropertyDrawer(typeof(ShowIfConditionAttribute))]
    public class ShowIfConditionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIfAttribute = attribute as ShowIfConditionAttribute;

            var targetField = property.serializedObject.FindProperty(showIfAttribute!.FieldName);

            if (targetField.boolValue)
                EditorGUI.PropertyField(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIfAttribute = attribute as ShowIfConditionAttribute;

            var targetField = property.serializedObject.FindProperty(showIfAttribute!.FieldName);

            if (targetField.boolValue)
                return EditorGUI.GetPropertyHeight(property, label);

            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
