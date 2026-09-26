using System;
using System.Runtime.InteropServices;

namespace Utility.Multithreading
{
    /// <summary>
    ///     Exposes the current OS (native) thread id. Unlike <see cref="Environment.CurrentManagedThreadId" />,
    ///     this value matches the thread list in a native crash dump (.dmp) and Sentry's native thread view,
    ///     so it can be cross-referenced when diagnosing hangs/deadlocks.
    /// </summary>
    public static class NativeThread
    {
        public static int CurrentId
        {
            get
            {
#if UNITY_STANDALONE_WIN
                return unchecked((int)GetCurrentThreadId());
#elif UNITY_STANDALONE_OSX
                pthread_threadid_np(IntPtr.Zero, out ulong threadId);
                return unchecked((int)threadId);
#else
                // No native thread id without per-platform P/Invoke; fall back so callers still get a stable id.
                return Environment.CurrentManagedThreadId;
#endif
            }
        }

#if UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
#elif UNITY_STANDALONE_OSX
        // current thread when the pthread_t argument is IntPtr.Zero
        [DllImport("libSystem.dylib")]
        private static extern int pthread_threadid_np(IntPtr thread, out ulong threadId);
#endif
    }
}
