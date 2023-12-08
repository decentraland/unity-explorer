using System;
using Utility;
using Utility.Multithreading;

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
            changeFrame = setAsChanged ? (int)MultithreadingUtility.FrameCount : int.MinValue;
            Value = value;
        }

        public bool ChangedThisFrame() =>
            changeFrame == MultithreadingUtility.FrameCount;

        public bool ChangedThisFrameTo(T value) =>
            changeFrame == MultithreadingUtility.FrameCount && EnumUtils.Equals(value, Value);

        public void Set(T value)
        {
            Value = value;
            changeFrame = (int)MultithreadingUtility.FrameCount;
        }

        public static implicit operator T(in EnumState<T> enumState) =>
            enumState.Value;
    }
}
