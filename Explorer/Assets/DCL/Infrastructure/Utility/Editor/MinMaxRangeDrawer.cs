using UnityEditor;
using UnityEngine;

namespace Utility.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    public class MinMaxRangeDrawer : PropertyDrawer
    {
        private const float FIELD_WIDTH = 50f;
        private const float SPACING = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var range = (MinMaxRangeAttribute)attribute;

            Vector2 value = property.vector2Value;
            float min = value.x;
            float max = value.y;

            position = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var minRect = new Rect(position.x, position.y, FIELD_WIDTH, position.height);

            var sliderRect = new Rect(
                position.x + FIELD_WIDTH + SPACING, position.y,
                position.width - 2f * (FIELD_WIDTH + SPACING), position.height);

            var maxRect = new Rect(position.xMax - FIELD_WIDTH, position.y, FIELD_WIDTH, position.height);

            EditorGUI.BeginChangeCheck();

            min = EditorGUI.FloatField(minRect, min);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.Min, range.Max);
            max = EditorGUI.FloatField(maxRect, max);

            if (EditorGUI.EndChangeCheck())
            {
                min = Mathf.Clamp(Mathf.Round(min * 100f) / 100f, range.Min, range.Max);
                max = Mathf.Clamp(Mathf.Round(max * 100f) / 100f, range.Min, range.Max);

                if (min > max) min = max;

                property.vector2Value = new Vector2(min, max);
            }

            EditorGUI.indentLevel = indent;
        }
    }
}
