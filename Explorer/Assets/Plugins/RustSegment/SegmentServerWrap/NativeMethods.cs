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
            FailInitializedWrongKey = 1,
            ErrorMessageTooLarge = 2,
            ErrorDeserialize = 3,
            ErrorNetwork = 4,
            ErrorUtf8Decode = 5,
        }

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void SegmentFfiCallback(ulong operationId, Response responseCode);

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_initialize")]
        internal static extern bool SegmentServerInitialize(
            IntPtr segmentWriteKey,
            SegmentFfiCallback callback
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

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_unflushed_batches_count")]
        internal static extern ulong SegmentServerUnFlushedBatchesCount();

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_flush")]
        internal static extern ulong SegmentServerFlush();

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_dispose")]
        internal static extern bool SegmentServerDispose();
    }
}
