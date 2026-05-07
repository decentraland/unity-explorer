// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    // WebGL friendly Interlocked to avoid threading issues
    public sealed class DCLInterlocked
    {
#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Read(ref long location)
        {
            return location;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Read(ref long location)
        {
            return Interlocked.Read(ref location);
        }
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Add(ref int location, int value)
        {
            location += value;
            return location;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Add(ref int location, int value)
        {
            return Interlocked.Add(ref location, value);
        }
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Increment(ref int location)
        {
            location = location + 1;
            return location;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Increment(ref int location) =>
            Interlocked.Increment(ref location);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Increment(ref long location)
        {
            location = location + 1;
            return location;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Increment(ref long location) =>
            Interlocked.Increment(ref location);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Exchange(ref int location, int value)
        {
            int previous = location;
            location = value;
            return previous;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Exchange(ref int location, int value)
        {
            return Interlocked.Exchange(ref location, value);
        }
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Exchange<T>(ref T location, T value) where T : class?
        {
            T previous = location;
            location = value;
            return previous;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Exchange<T>(ref T location, T value) where T : class? =>
            Interlocked.Exchange(ref location, value);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareExchange(ref int location, int value, int comparand)
        {
            int previous = location;
            if (previous == comparand) location = value;
            return previous;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareExchange(ref int location, int value, int comparand)
        {
            return Interlocked.CompareExchange(ref location, value, comparand);
        }
#endif
    }
}
