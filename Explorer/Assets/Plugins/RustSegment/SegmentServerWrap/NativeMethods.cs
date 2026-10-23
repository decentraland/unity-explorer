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
            ErrorDiskFull = 2,
        }

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void SegmentFfiCallback(ulong operationId, Response responseCode);

        /// <summary>
        ///     How the native queue keeps the events of the session, decided once by <see cref="SegmentServerInitialize" />.
        ///     The values are part of the C ABI.
        /// </summary>
        internal enum QueueHealth : byte
        {
            /// <summary>This process owns the queue file; the events are kept on disk.</summary>
            Persistent = 0,

            /// <summary>Another process owns the queue file; the events of this process stay in memory.</summary>
            OwnedByAnotherInstance = 1,

            /// <summary>The queue file was corrupt and has been replaced by an empty one.</summary>
            Recreated = 2,

            /// <summary>The queue file is given up for the session: the disk is full.</summary>
            DegradedDiskFull = 3,

            /// <summary>The queue file is given up for the session: it cannot be written.</summary>
            DegradedReadOnly = 4,

            /// <summary>The queue file is given up for the session: the disk failed.</summary>
            DegradedIoError = 5,

            /// <summary>The queue file is given up for the session: the OS refused access.</summary>
            DegradedPermissionDenied = 6,

            /// <summary>The queue file is given up for the session: it cannot be opened.</summary>
            DegradedCannotOpen = 7,

            /// <summary>The queue file is given up for the session: it could not be recreated after it was found corrupt.</summary>
            DegradedCorrupt = 8,

            /// <summary>
            ///     The queue file is given up for the session: it failed in a way that says nothing about the file or its
            ///     environment, reported on the error channel.
            /// </summary>
            DegradedOther = 9,

            /// <summary>
            ///     This call did not start the server: it already runs. In debug builds also returned when the initialization
            ///     panicked (reported on the error channel); release builds abort on panic.
            /// </summary>
            NotInitialized = 255,
        }

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void SegmentFfiErrorCallback(IntPtr msg);

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_initialize")]
        internal static extern QueueHealth SegmentServerInitialize(
            IntPtr queueFilePath,
            uint queueCountLimit,
            IntPtr segmentWriteKey,
            IntPtr segmentApiHost,
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

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_pump_next_event")]
        internal static extern int SegmentServerPumpNextEvent(); // 1 - pumped, 0 - nothing

        [DllImport(LIBRARY_NAME, CallingConvention = CALLING_CONVENTION, CharSet = CHAR_SET, EntryPoint = "segment_server_dispose")]
        internal static extern bool SegmentServerDispose();
    }
}
