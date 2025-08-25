using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Decentraland.Terrain
{
    [CustomPropertyDrawer(typeof(EnumIndexedArrayAttribute))]
    public sealed class EnumIndexedArrayDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, string[]> enumNameCache = new ();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (EnumIndexedArrayAttribute)this.attribute;
            Type enumType = attribute.EnumType;

            if (!enumNameCache.TryGetValue(enumType, out string[] enumNames))
            {
                enumNames = Enum.GetNames(enumType);
                enumNameCache.Add(enumType, enumNames);
            }

            // Feels like a hack, but I could not find a better way to find the index of the current
            // element.
            int lastSpace = label.text.LastIndexOf(' ');

            if (lastSpace >= 0 && int.TryParse(label.text.Substring(lastSpace + 1), out int index)
                               && index < enumNames.Length) { label.text = enumNames[index]; }

            EditorGUI.PropertyField(position, property, label);
        }
    }
}
