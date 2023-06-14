using System;
using Utility;

namespace ECS.Abstract
{
    /// <summary>
    ///     Allows systems to <b>react</b> on enum changed this frame
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct EnumState<T> where T: unmanaged, Enum
    {
        private bool changedThisFrame;

        public T Value { get; private set; }

        public EnumState(T value, bool setAsChanged = false)
        {
            changedThisFrame = setAsChanged;
            Value = value;
        }

        public bool ChangedThisFrame() =>
            changedThisFrame;

        public bool ChangedThisFrameTo(T value) =>
            changedThisFrame && EnumUtils.Equals(value, Value);

        public void Set(T value)
        {
            Value = value;
            changedThisFrame = true;
        }

        public void SetFramePassed()
        {
            changedThisFrame = false;
        }

        public static implicit operator T(in EnumState<T> enumState) =>
            enumState.Value;
    }
}
