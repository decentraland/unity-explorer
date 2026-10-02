using System;

namespace DCL.Utility.Types
{
    /// <summary>Eight inline slots of any <typeparamref name="T"/>: a value type that copies with its owner and never allocates.</summary>
    public struct Block8<T>
    {
        public const int CAPACITY = 8;

        private T s0, s1, s2, s3, s4, s5, s6, s7;

        public T this[int index]
        {
            readonly get =>
                index switch
                {
                    0 => s0,
                    1 => s1,
                    2 => s2,
                    3 => s3,
                    4 => s4,
                    5 => s5,
                    6 => s6,
                    7 => s7,
                    _ => throw new ArgumentOutOfRangeException(nameof(index), index, $"Block8 holds {CAPACITY} slots"),
                };

            set
            {
                switch (index)
                {
                    case 0: s0 = value; return;
                    case 1: s1 = value; return;
                    case 2: s2 = value; return;
                    case 3: s3 = value; return;
                    case 4: s4 = value; return;
                    case 5: s5 = value; return;
                    case 6: s6 = value; return;
                    case 7: s7 = value; return;
                    default: throw new ArgumentOutOfRangeException(nameof(index), index, $"Block8 holds {CAPACITY} slots");
                }
            }
        }
    }
}
