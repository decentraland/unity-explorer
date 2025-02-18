using System;
using System.Runtime.InteropServices;

namespace Plugins.DclNativeMutex
{
    internal static class DclMutexNativeMethods
    {
        private const string LIB_NAME = "libdcl_mutex.dylib";

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe IntPtr dcl_mutex_new(string name, int* error);

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dcl_mutex_wait(IntPtr mutex);

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dcl_mutex_release(IntPtr mutex);

        [DllImport(LIB_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dcl_mutex_close_handle(IntPtr mutex);
    }
}
