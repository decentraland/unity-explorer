using Unity.Profiling;

#if ENABLE_PROFILER
namespace DCL.Profiling
{
    public class NetworkProfilerCounters
    {
        public const string CATEGORY_NAME = "DCL Network";

        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        // OS Total IPv4
        public static readonly string TOTAL_BYTES_SENT_NAME = "Total Bytes Send";
        public static readonly string TOTAL_BYTES_RECEIVED_NAME = "Total Bytes Received";
        public static readonly string TOTAL_FRAME_BYTES_SENT_NAME = "Total Frame Bytes Send";
        public static readonly string TOTAL_FRAME_BYTES_RECEIVED_NAME = "Total Frame Bytes Received";

        public static readonly ProfilerCounterValue<ulong> TOTAL_BYTES_SENT
            = new (CATEGORY, TOTAL_BYTES_SENT_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> TOTAL_BYTES_RECEIVED
            = new (CATEGORY, TOTAL_BYTES_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> TOTAL_FRAME_BYTES_SENT
            = new (CATEGORY, TOTAL_FRAME_BYTES_SENT_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> TOTAL_FRAME_BYTES_RECEIVED
            = new (CATEGORY, TOTAL_FRAME_BYTES_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        // LiveKit
        public static readonly string LIVEKIT_ISLAND_SEND_NAME = "LiveKit Island Send";
        public static readonly string LIVEKIT_SCENE_SEND_NAME = "LiveKit Scene Send";
        public static readonly string LIVEKIT_ISLAND_RECEIVED_NAME = "LiveKit Island Received";
        public static readonly string LIVEKIT_SCENE_RECEIVED_NAME = "LiveKit Scene Received";

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_SEND
            = new (CATEGORY, LIVEKIT_ISLAND_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_RECEIVED
            = new (CATEGORY, LIVEKIT_ISLAND_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_SEND
            = new (CATEGORY, LIVEKIT_SCENE_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_RECEIVED
            = new (CATEGORY, LIVEKIT_SCENE_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        // WebRequests
        public static readonly string WEB_REQUESTS_UPLOADED_NAME = "WebRequests Uploaded";
        public static readonly string WEB_REQUESTS_DOWNLOADED_NAME = "WebRequests Downloaded";
        public static readonly string WEB_REQUESTS_UPLOADED_FRAME_NAME = "WebRequests Frame Uploaded";
        public static readonly string WEB_REQUESTS_DOWNLOADED_FRAME_NAME = "WebRequests Frame Downloaded";

        public static readonly ProfilerCounterValue<ulong> WEB_REQUESTS_UPLOADED
            = new (CATEGORY, WEB_REQUESTS_UPLOADED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WEB_REQUESTS_DOWNLOADED
            = new (CATEGORY, WEB_REQUESTS_DOWNLOADED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WEB_REQUESTS_UPLOADED_FRAME
            = new (CATEGORY, WEB_REQUESTS_UPLOADED_FRAME_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WEB_REQUESTS_DOWNLOADED_FRAME
            = new (CATEGORY, WEB_REQUESTS_DOWNLOADED_FRAME_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);
    }
}
#endif
