using System;
using UnityEditor;
using UnityEngine;

namespace DCL.PluginSystem.Editor
{
    [CustomPropertyDrawer(typeof(PluginSettingsTitleAttribute))]
    public class PluginSettingsTitleDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            object managedRef = property.managedReferenceValue;

            if (managedRef == null)
                EditorGUI.PropertyField(position, property, label, true);
            else
            {
                // the label is either the class name itself or the contained type name + class name
                Type type = managedRef.GetType();

                label = new GUIContent(type.DeclaringType != null ? type.DeclaringType.Name + "." + type.Name : type.Name);
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);
    }
}
