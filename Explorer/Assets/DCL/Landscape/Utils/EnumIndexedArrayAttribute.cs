using System;
using UnityEngine;

namespace DCL.Landscape.Utils
{
    public sealed class EnumIndexedArrayAttribute : PropertyAttribute
    {
        public Type EnumType { get; }

        public EnumIndexedArrayAttribute(Type enumType)
        {
            EnumType = enumType;
        }
    }
}
