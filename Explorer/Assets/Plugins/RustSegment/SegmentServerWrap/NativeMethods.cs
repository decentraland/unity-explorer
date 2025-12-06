using System;
using System.Runtime.InteropServices;

namespace Plugins.RustSegment.SegmentServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "segment-server";
        private const CharSet CHAR_SET = CharSet.Ansi;
        private const CallingConvention CALLING_CONVENTION = CallingConvention.Cdecl;

        internal enum Response : byte
        {
            Success = 0,
            Error = 1,
        }

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void SegmentFfiCallback(ulong operationId, Response responseCode);

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void SegmentFfiErrorCallback(IntPtr msg);

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_initialize")]
        internal static extern bool SegmentServerInitialize(
            IntPtr queueFilePath,
            uint queueCountLimit,
            IntPtr segmentWriteKey,
            SegmentFfiCallback callback,
            SegmentFfiErrorCallback errorCallback
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_identify")]
        internal static extern ulong SegmentServerIdentify(
            IntPtr usedId,
            IntPtr anonId,
            IntPtr traitsJson,
            IntPtr contextJson
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_track")]
        internal static extern ulong SegmentServerTrack(
            IntPtr usedId,
            IntPtr anonId,
            IntPtr eventName,
            IntPtr propertiesJson,
            IntPtr contextJson
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_instant_track_and_flush")]
        internal static extern ulong SegmentServerInstantTrackAndFlush(
            IntPtr usedId,
            IntPtr anonId,
            IntPtr eventName,
            IntPtr propertiesJson,
            IntPtr contextJson
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_flush")]
        internal static extern ulong SegmentServerFlush();

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_dispose")]
        internal static extern bool SegmentServerDispose();
    }
}
