using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Utility
{
    /// <summary>
    ///     Contains non-allocating generic versions of enum utility functions
    /// </summary>
    public static class EnumUtils
    {
        public static IEqualityComparer<T> GetEqualityComparer<T>() where T: unmanaged, Enum =>
            EqualityComparer<T>.INSTANCE;

        public static unsafe int GetHashCode<T>(T @enum) where T: unmanaged, Enum
        {
            return sizeof(T) switch
                   {
                       sizeof(byte) => *(byte*)&@enum,
                       sizeof(short) => *(short*)&@enum,
                       sizeof(int) => *(int*)&@enum,
                       sizeof(long) => (*(long*)&@enum).GetHashCode(),
                       _ => 0,
                   };
        }

        public static unsafe bool Equals<T>(T x, T y) where T: unmanaged, Enum
        {
            return sizeof(T) switch
                   {
                       sizeof(byte) => *(byte*)&x == *(byte*)&y,
                       sizeof(short) => *(short*)&x == *(short*)&y,
                       sizeof(int) => *(int*)&x == *(int*)&y,
                       sizeof(long) => *(long*)&x == *(long*)&y,
                       _ => false,
                   };
        }

        public static unsafe int ToInt<T>(T @enum) where T: unmanaged, Enum
        {
            return sizeof(T) switch
                   {
                       sizeof(byte) => *(byte*)&@enum,
                       sizeof(short) => *(short*)&@enum,
                       sizeof(int) => *(int*)&@enum,
                       sizeof(long) => (int)*(long*)&@enum,
                       _ => 0,
                   };
        }

        public static unsafe T FromInt<T>(int value) where T: unmanaged, Enum
        {
            switch (sizeof(T))
            {
                case sizeof(byte):
                    var @byte = (byte)value;
                    return Unsafe.As<byte, T>(ref @byte);
                case sizeof(short):
                    var @short = (short)value;
                    return Unsafe.As<short, T>(ref @short);
                case sizeof(int):
                    return Unsafe.As<int, T>(ref value);
                case sizeof(long):
                    var @long = (long)value;
                    return Unsafe.As<long, T>(ref @long);
                default: return default(T);
            }
        }

        public static unsafe bool HasExactlyOneFlag<T>(this T value) where T: unmanaged, Enum
        {
            return sizeof(T) switch
                   {
                       sizeof(byte) => (*(byte*)&value & (*(byte*)&value - 1)) == 0,
                       sizeof(short) => (*(short*)&value & (*(short*)&value - 1)) == 0,
                       sizeof(int) => (*(int*)&value & (*(int*)&value - 1)) == 0,
                       sizeof(long) => (*(long*)&value & (*(long*)&value - 1)) == 0,
                       _ => false,
                   };
        }

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

        private class EqualityComparer<T> : IEqualityComparer<T> where T: unmanaged, Enum
        {
            public static readonly EqualityComparer<T> INSTANCE = new ();

            public bool Equals(T x, T y) =>
                EnumUtils.Equals(x, y);

            public int GetHashCode(T obj) =>
                EnumUtils.GetHashCode(obj);
        }
    }
}
