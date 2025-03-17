using System;
using System.Collections.Generic;

namespace Utility
{
    public static class CanBeDirty
    {
        public static CanBeDirty<T> FromEnum<T>(T defaultValue = default) where T: unmanaged, Enum =>
            new (defaultValue, EnumUtils.GetEqualityComparer<T>());
    }

    public struct CanBeDirty<T> where T: struct
    {
        private T value;

        private IEqualityComparer<T> equalityComparer;

        public CanBeDirty(T value, IEqualityComparer<T> equalityComparer = null)
        {
            this.value = value;
            IsDirty = false;

            this.equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
        }

        /// <summary>
        ///     Is Dirty is supposed to be preserved during the whole frame
        ///     and gets reset when the same value is set again
        /// </summary>
        public bool IsDirty { get; private set; }

        public T Value
        {
            get => value;

            set
            {
                equalityComparer ??= EqualityComparer<T>.Default;

                if (equalityComparer.Equals(this.value, value))
                {
                    IsDirty = false;
                    return;
                }

                this.value = value;
                IsDirty = true;
            }
        }

        public static implicit operator T(CanBeDirty<T> canBeDirty) =>
            canBeDirty.Value;
    }
}
