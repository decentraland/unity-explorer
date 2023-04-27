using System;
using System.Runtime.CompilerServices;

namespace Utility
{
    /// <summary>
    /// Contains non-allocating generic versions of enum utility functions
    /// </summary>
    public static class EnumUtils
    {
        public static unsafe bool HasFlag<T>(T x, T y) where T : unmanaged, Enum
        {
            switch (sizeof(T))
            {
                case sizeof(byte):
                    return (*(byte*) &x & *(byte*) &y) != 0;

                case sizeof(short):
                    return (*(short*) &x & *(short*) &y) != 0;

                case sizeof(int):
                    return (*(int*) &x & *(int*) &y) != 0;

                case sizeof(long):
                    return (*(long*) &x & *(long*) &y) != 0L;

                default:
                    return false;
            }
        }

        public static T[] Values<T>() =>
            (T[]) Enum.GetValues(typeof(T));
    }
}
