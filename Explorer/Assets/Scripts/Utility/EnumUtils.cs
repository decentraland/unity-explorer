using System;
using System.Runtime.CompilerServices;

namespace Utility
{
    /// <summary>
    ///     Contains non-allocating generic versions of enum utility functions
    /// </summary>
    public static class EnumUtils
    {
        public static unsafe bool HasFlag<T>(T x, T y) where T: unmanaged, Enum
        {
            switch (sizeof(T))
            {
                case sizeof(byte):
                    return (*(byte*)&x & *(byte*)&y) != 0;

                case sizeof(short):
                    return (*(short*)&x & *(short*)&y) != 0;

                case sizeof(int):
                    return (*(int*)&x & *(int*)&y) != 0;

                case sizeof(long):
                    return (*(long*)&x & *(long*)&y) != 0L;

                default:
                    return false;
            }
        }

        public static unsafe void RemoveFlag<T>(this ref T x, T y) where T: unmanaged, Enum
        {
            // Check if the underlying type of the enum is byte, short, int, or long
            switch (sizeof(T))
            {
                case sizeof(byte):
                {
                    ref byte xRef = ref Unsafe.As<T, byte>(ref x);
                    ref byte yRef = ref Unsafe.As<T, byte>(ref y);
                    xRef &= (byte)~yRef;
                }

                    return;
                case sizeof(short):
                {
                    ref short xRef = ref Unsafe.As<T, short>(ref x);
                    ref short yRef = ref Unsafe.As<T, short>(ref y);
                    xRef &= (short)~yRef;
                }

                    return;
                case sizeof(int):
                {
                    ref int xRef = ref Unsafe.As<T, int>(ref x);
                    ref int yRef = ref Unsafe.As<T, int>(ref y);
                    xRef &= ~yRef;
                }

                    return;
                case sizeof(long):
                {
                    ref long xRef = ref Unsafe.As<T, long>(ref x);
                    ref long yRef = ref Unsafe.As<T, long>(ref y);
                    xRef &= ~yRef;
                }

                    return;
            }
        }

        public static T[] Values<T>() =>
            (T[])Enum.GetValues(typeof(T));
    }
}
