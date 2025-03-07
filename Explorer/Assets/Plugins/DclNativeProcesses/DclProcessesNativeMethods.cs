using System;
using System.Runtime.InteropServices;

namespace Plugins.DclNativeProcesses
{
    /// <summary>
    /// IL2CPP doesn't fully support Process functionality
    /// </summary>
    internal static class DclProcessesNativeMethods
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        private const string LIB_NAME = "DCLProcesses.dll";
#else
        private const string LIB_NAME = "libDCLProcesses.dylib";
#endif
        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_process_name(int pid);

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int free_name(IntPtr name);
    }

    public readonly struct ProcessName : IDisposable
    {
        public readonly string Name;
        private readonly IntPtr ptr;

        public bool IsEmpty => ptr == IntPtr.Zero;

        public ProcessName(int pid)
        {
            ptr = DclProcessesNativeMethods.get_process_name(pid);

            if (ptr == IntPtr.Zero)
            {
                Name = string.Empty;
                return;
            }

            Name = Marshal.PtrToStringAnsi(ptr);
        }

        public void Dispose()
        {
            if (ptr != IntPtr.Zero)
                DclProcessesNativeMethods.free_name(ptr);
        }
    }
}
