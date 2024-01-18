using System;

namespace Utility
{
    public struct CanBeDirty<T> where T: struct, IEquatable<T>
    {
        private T value;

        public CanBeDirty(T value)
        {
            this.value = value;
            IsDirty = false;
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
                if (value.Equals(this.value))
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
