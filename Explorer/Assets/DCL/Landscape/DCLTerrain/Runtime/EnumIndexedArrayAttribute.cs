using System;
using UnityEngine;

namespace Decentraland.Terrain
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
