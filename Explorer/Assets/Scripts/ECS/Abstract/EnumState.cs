using System;
using UnityEngine;
using Utility;

namespace ECS.Abstract
{
    /// <summary>
    ///     Allows systems to <b>react</b> on enum changed this frame
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct EnumState<T> where T: unmanaged, Enum
    {
        private int changeFrame;

        public T Value { get; private set; }

        public EnumState(T value, bool setAsChanged = false)
        {
            changeFrame = setAsChanged ? Time.frameCount : int.MinValue;
            Value = value;
        }

        public bool ChangedThisFrame() =>
            changeFrame == Time.frameCount;

        public bool ChangedThisFrameTo(T value) =>
            changeFrame == Time.frameCount && EnumUtils.Equals(value, Value);

        public void Set(T value)
        {
            Value = value;
            changeFrame = Time.frameCount;
        }

        public static implicit operator T(in EnumState<T> enumState) =>
            enumState.Value;
    }
}
