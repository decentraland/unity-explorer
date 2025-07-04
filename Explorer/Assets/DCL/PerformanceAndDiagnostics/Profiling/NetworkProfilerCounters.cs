using Unity.Profiling;

namespace DCL.Profiling
{
    // Don't put it under ENABLE_PROFILER define, Unity keeps the profiler counters contract but with an empty body under the define
    public class NetworkProfilerCounters
    {
        public const string CATEGORY_NAME = "DCL Network";

        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        // full OS traffic for Wifi IPv4
        public const string WIFI_IPV4_BYTES_SENT_NAME = "Wifi IPv4 Sent";
        public const string WIFI_IPV4_BYTES_RECEIVED_NAME = "Wifi IPv4 Received";
        public const string WIFI_IPV4_BYTES_SENT_FRAME_NAME = "Wifi IPv4 Frame Sent";
        public const string WIFI_IPV4_BYTES_RECEIVED_FRAME_NAME = "Wifi IPv4 Frame Received";
        public const string WIFI_IPV4_MBPS_SENT_NAME = "Wifi IPv4 Mbps Sent";
        public const string WIFI_IPV4_MBPS_RECEIVED_NAME = "Wifi IPv4 Mbps Received";

        public static readonly ProfilerCounterValue<ulong> WIFI_IPV4_BYTES_SENT
            = new (CATEGORY, WIFI_IPV4_BYTES_SENT_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WIFI_IPV4_BYTES_RECEIVED
            = new (CATEGORY, WIFI_IPV4_BYTES_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WIFI_IPV4_BYTES_FRAME_SENT
            = new (CATEGORY, WIFI_IPV4_BYTES_SENT_FRAME_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> WIFI_IPV4_BYTES_FRAME_RECEIVED
            = new (CATEGORY, WIFI_IPV4_BYTES_RECEIVED_FRAME_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<float> WIFI_IPV4_MBPS_SENT
            = new (CATEGORY, WIFI_IPV4_MBPS_SENT_NAME, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<float> WIFI_IPV4_MBPS_RECEIVED
            = new (CATEGORY, WIFI_IPV4_MBPS_RECEIVED_NAME, ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        // only LiveKit payloads (headers are not tracked)
        public const string LIVEKIT_ISLAND_SEND_NAME = "LiveKit Island Sent";
        public const string LIVEKIT_SCENE_SEND_NAME = "LiveKit Scene Sent";
        public const string LIVEKIT_ISLAND_RECEIVED_NAME = "LiveKit Island Received";
        public const string LIVEKIT_SCENE_RECEIVED_NAME = "LiveKit Scene Received";

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_SEND
            = new (CATEGORY, LIVEKIT_ISLAND_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_RECEIVED
            = new (CATEGORY, LIVEKIT_ISLAND_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_SEND
            = new (CATEGORY, LIVEKIT_SCENE_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_RECEIVED
            = new (CATEGORY, LIVEKIT_SCENE_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        // WebRequests
        public const string WEB_REQUESTS_UPLOADED_NAME = "WebRequests Uploaded";
        public const string WEB_REQUESTS_DOWNLOADED_NAME = "WebRequests Downloaded";
        public const string WEB_REQUESTS_UPLOADED_FRAME_NAME = "WebRequests Frame Uploaded";
        public const string WEB_REQUESTS_DOWNLOADED_FRAME_NAME = "WebRequests Frame Downloaded";

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
