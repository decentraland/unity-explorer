using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public static class NativeMethodsProcessesHub
    {
        private const string LIBRARY_NAME = "libprocesseshub";
        private const string PREFIX = "processeshub_";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "start")]
        internal extern static int ProcessesHubStart(string processExePath);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "is_running")]
        internal extern static int ProcessesHubIsRunning();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "stop")]
        internal extern static int ProcessesHubStop();
    }

#else

    public static class NativeMethodsProcessesHub
    {
        internal static int ProcessesHubStart(string processExePath) =>
            throw new PlatformNotSupportedException();

        internal static int ProcessesHubIsRunning() =>
            throw new PlatformNotSupportedException();

        internal static int ProcessesHubStop() =>
            throw new PlatformNotSupportedException();
    }

#endif
}
