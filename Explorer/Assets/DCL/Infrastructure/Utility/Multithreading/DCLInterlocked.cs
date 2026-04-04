// TRUST_WEBGL_THREAD_SAFETY_FLAG

#if !UNITY_WEBGL
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#endif

using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Read(ref long location)
        {
#if UNITY_WEBGL
            return location;
#else
            return Interlocked.Read(ref location);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Add(ref int location, int value)
        {
#if UNITY_WEBGL
            location += value;
            return location;
#else
            return Interlocked.Add(ref location, value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Increment(ref int location)
        {
#if UNITY_WEBGL
            location = location + 1;
            return location;
#else
            return Interlocked.Increment(ref location);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Increment(ref ulong location)
        {
#if UNITY_WEBGL
            location += 1;
            return location;
#else
            Interlocked.Increment(ref location);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Exchange(ref int location, int value)
        {
#if UNITY_WEBGL
            int previous = location;
            location = value;
            return previous;
#else
            return Interlocked.Exchange(ref location, value);
#endif
        }
    }
}
