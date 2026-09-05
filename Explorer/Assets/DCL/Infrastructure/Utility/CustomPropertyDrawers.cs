using System;
using UnityEngine;

namespace Utility
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SDKParcelPositionHelper : PropertyAttribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfEnumAttribute : PropertyAttribute
    {
        public readonly string FieldName;
        public readonly int[] States;

        public ShowIfEnumAttribute(string fieldName, params int[] states)
        {
            FieldName = fieldName;
            States = states;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfConditionAttribute : PropertyAttribute
    {
        public readonly string FieldName;

        public ShowIfConditionAttribute(string fieldName)
        {
            FieldName = fieldName;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ShowOnlyAttribute : PropertyAttribute { }

    /// <summary>
    /// Displays a visible info box above a field in the Inspector.
    /// Use instead of [Tooltip] when the note must always be visible.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class NoteAttribute : PropertyAttribute
    {
        public readonly string Text;

        public NoteAttribute(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Draws a Vector2 field as a min-max range slider.
    /// x = min value, y = max value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class MinMaxRangeAttribute : PropertyAttribute
    {
        public readonly float Min;
        public readonly float Max;

        public MinMaxRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}
