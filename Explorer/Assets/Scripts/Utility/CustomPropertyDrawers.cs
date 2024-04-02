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
}
