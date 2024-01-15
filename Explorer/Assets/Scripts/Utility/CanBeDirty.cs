using System;

namespace Utility
{
    public struct CanBeDirty<T> where T: struct, IEquatable<T>
    {
        private T value;

        public bool IsDirty { get; private set; }

        public T Value
        {
            get => value;

            set
            {
                if (value.Equals(this.value))
                    return;

                this.value = value;
                IsDirty = true;
            }
        }
    }
}
