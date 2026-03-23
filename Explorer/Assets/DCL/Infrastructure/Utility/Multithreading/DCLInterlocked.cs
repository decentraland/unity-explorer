// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
    /// <summary>
    ///     WebGL-compatible replacement for <see cref="System.Threading.Interlocked" /> atomic operations.
    ///     On non-WebGL platforms the real <c>Interlocked</c> methods are used to guarantee memory ordering
    ///     across threads. On WebGL (single-threaded) direct non-atomic operations are used instead, since
    ///     there are no competing threads and the interlocked overhead is unnecessary.
    ///     <para>The <c>TRUST_WEBGL_THREAD_SAFETY_FLAG</c> comment at the top of the file marks this class as
    ///     intentionally unsafe on multi-threaded platforms — the WebGL path must never be used outside WebGL.</para>
    /// </summary>
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
        public static ulong Increment(ref ulong location)
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
    }
}
