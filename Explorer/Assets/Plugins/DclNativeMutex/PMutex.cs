using DCL.Diagnostics;
using System;
using System.Runtime.InteropServices;

namespace Plugins.DclNativeMutex
{
    /// <summary>
    /// Created due named mutex is not supported by IL2CPP
    /// </summary>
    public class PMutex : IDisposable
    {
        /// <summary>
        /// From https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject
        /// </summary>
        private const uint WAIT_OBJECT_0 = 0x00000000;

        // Unix
        private const int O_CREAT = 512; // Create semaphore if it does not exist
        private const int SEM_PERM = 777;

        private readonly string name;
        private readonly IntPtr pMutex;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMillisecond);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReleaseMutex(IntPtr hMutex);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
#endif

        public PMutex(string name)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            pMutex = CreateMutex(IntPtr.Zero, false, name);

            if (pMutex == IntPtr.Zero)
                throw new Exception("Failed to create mutex");
#else
            unsafe
            {
                var error = 0;
                pMutex = DclMutexNativeMethods.dcl_mutex_new(name, &error);
                ReportHub.Log(ReportData.UNSPECIFIED, $"Mutex p {pMutex.ToInt64()}");

                if (error != 0)
                    throw new Exception($"Failed to create mutex with code {error}");
            }
#endif
        }

        public void WaitOne(uint milliseconds)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            uint result = WaitForSingleObject(pMutex, milliseconds);

            if (result != WAIT_OBJECT_0)
                throw new Exception($"Cannot acquire mutex with result {result}");
#else
            int result = DclMutexNativeMethods.dcl_mutex_wait(pMutex);

            if (result != 0)
                throw new Exception($"Cannot acquire mutex with result {result}");
#endif
        }

        public void ReleaseMutex()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            bool result = ReleaseMutex(pMutex);

            if (result == false)
                throw new Exception("Cannot release mutex");
#else
            int result = DclMutexNativeMethods.dcl_mutex_release(pMutex);

            if (result != 0)
                throw new Exception($"Cannot release mutex with result {result}");
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
            CloseHandle(pMutex);
#else
            DclMutexNativeMethods.dcl_mutex_close_handle(pMutex);
#endif
        }
    }
}
