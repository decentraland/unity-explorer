using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
    enum PH_Error : int
    {
        Ok = 0,
        ProcessIsRunning = 1,
        CannotStartProcess = 2,
        ProcessIsNotRunning = 3,
    };

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public static class NativeMethodsProcessesHub
    {
        private const string LIBRARY_NAME = "libprocesseshub";
        private const string PREFIX = "processeshub_";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "start")]
        internal extern static PH_Error ProcessesHubStart(string processExePath);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "is_running")]
        internal extern static bool ProcessesHubIsRunning();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "stop")]
        internal extern static PH_Error ProcessesHubStop();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "used_ram")]
        internal extern static ulong ProcessesUsedRAM();
    }

#else

    public static class NativeMethodsProcessesHub
    {
        internal static PH_Error ProcessesHubStart(string processExePath) =>
            throw new PlatformNotSupportedException();

        internal static bool ProcessesHubIsRunning() =>
            throw new PlatformNotSupportedException();

        internal static PH_Error ProcessesHubStop() =>
            throw new PlatformNotSupportedException();

        internal static ulong ProcessesUsedRAM() =>
            0;
    }

#endif
}
