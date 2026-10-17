// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System.Runtime.CompilerServices;
using System.Threading;

namespace Utility.Multithreading
{
    // WebGL friendly Volatile to avoid threading issues
    public static class DCLVolatile
    {
#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(ref int location) => location;
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(ref int location) => Volatile.Read(ref location);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Read(ref uint location) => location;
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Read(ref uint location) => Volatile.Read(ref location);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Read(ref long location) => location;
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Read(ref long location) => Volatile.Read(ref location);
#endif

#if UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(ref T location) where T : class? => location;
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(ref T location) where T : class? => Volatile.Read(ref location);
#endif
    }
}
