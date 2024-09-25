using System.Runtime.InteropServices;

namespace Plugins.RustSegment.SegmentServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "segment-server";

        internal enum Response : byte
        {
            Success = 0,
            FailInitializedWrongKey = 1,
            ErrorMessageTooLarge = 2,
            ErrorDeserialize = 3,
            ErrorNetwork = 4,
            ErrorUtf8Decode = 5,
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SegmentFfiCallback(ulong operationId, Response responseCode);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "segment_server_initialize")]
        internal extern static bool SegmentServerInitialize(
            string segmentWriteKey,
            SegmentFfiCallback callback
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "segment_server_identify")]
        internal extern static ulong SegmentServerIdentify(
            string usedId,
            string traitsJson,
            string contextJson
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "segment_server_track")]
        internal extern static ulong SegmentServerTrack(
            string usedId,
            string eventName,
            string propertiesJson,
            string contextJson
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "segment_server_flush")]
        internal extern static ulong SegmentServerFlush();
    }
}
