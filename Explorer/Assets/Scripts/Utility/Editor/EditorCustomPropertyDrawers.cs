#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Utility.Editor
{
    [CustomPropertyDrawer(typeof(SDKParcelPositionHelper))]
    public class SDKParcelPositionHelperDrawer : PropertyDrawer
    {
        private readonly Dictionary<string, Vector2Int> scenes = new ()
        {
            { "Cube Spawner", new Vector2Int(0, 0) },
            { "Cube Wave 32x32", new Vector2Int(0, 2) },
            { "Scene bounds check", new Vector2Int(5, 90) },
            { "Testing Gallery", new Vector2Int(52, -52) },
            { "Testing 3D Models", new Vector2Int(54, -55) },
            { "UI Backgrounds", new Vector2Int(70, -9) },
            { "dbmonster", new Vector2Int(73, -2) },
            { "Animator Tests Shark", new Vector2Int(73, -8) },
            { "UI Canvas Information", new Vector2Int(76, -10) },
            { "Raycast unit tests", new Vector2Int(77, -1) },
            { "Tweens", new Vector2Int(77, -5) },
            { "Portable Experience", new Vector2Int(8, 8) },
            { "Portable Experience hide UI", new Vector2Int(8, 7) },
            { "Portable Experience disabled", new Vector2Int(8, 9) },
            { "Main crdt", new Vector2Int(80, -2) },
            { "UI", new Vector2Int(80, -3) },
            { "Restricted Actions", new Vector2Int(80, -4) },
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect propertyPosition = EditorGUI.PrefixLabel(position, label);
            var buttonWidth = 80f;
            var space = 5f;
            var buttonPosition = new Rect(propertyPosition.x, propertyPosition.y, buttonWidth, propertyPosition.height * 0.5f);
            var fieldPosition = new Rect(space + propertyPosition.x + buttonWidth, propertyPosition.y, propertyPosition.width - buttonWidth - space, propertyPosition.height);

            EditorGUI.PropertyField(fieldPosition, property, GUIContent.none);

            if (GUI.Button(buttonPosition, "Select"))
            {
                var menu = new GenericMenu();

                foreach ((string key, Vector2Int value) in scenes)
                {
                    string valueName = key;
                    Vector2Int vector = value;

                    menu.AddItem(new GUIContent($"{valueName} {value}"), false, () =>
                    {
                        property.vector2IntValue = vector;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.ShowAsContext();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            base.GetPropertyHeight(property, label) + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    [CustomPropertyDrawer(typeof(ShowIfEnumAttribute))]
    public class ShowIfEnumDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIfAttribute = attribute as ShowIfEnumAttribute;

            SerializedProperty targetField = property.serializedObject.FindProperty(showIfAttribute!.FieldName);
            int intValue = targetField.intValue;

            if (showIfAttribute.States.Contains(intValue))
                EditorGUI.PropertyField(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIfAttribute = attribute as ShowIfEnumAttribute;

            SerializedProperty targetField = property.serializedObject.FindProperty(showIfAttribute!.FieldName);
            int intValue = targetField.intValue;

            if (showIfAttribute.States.Contains(intValue))
                return EditorGUI.GetPropertyHeight(property, label);

            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }

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
#endif
