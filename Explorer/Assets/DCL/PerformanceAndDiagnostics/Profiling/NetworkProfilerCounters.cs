using Unity.Profiling;

#if ENABLE_PROFILER
namespace DCL.Profiling
{
    public class NetworkProfilerCounters
    {
        public const string CATEGORY_NAME = "DCL Network";

        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        public static readonly string TOTAL_BYTES_SENT_NAME = "Total Bytes Send";
        public static readonly string TOTAL_BYTES_RECEIVED_NAME = "Total Bytes Received";
        public static readonly string LIVEKIT_ISLAND_SEND_NAME = "LiveKit Island Send";
        public static readonly string LIVEKIT_SCENE_SEND_NAME = "LiveKit Scene Send";
        public static readonly string LIVEKIT_ISLAND_RECEIVED_NAME = "LiveKit Island Received";
        public static readonly string LIVEKIT_SCENE_RECEIVED_NAME = "LiveKit Scene Received";

        public static readonly ProfilerCounterValue<ulong> TOTAL_BYTES_SENT
            = new (CATEGORY, TOTAL_BYTES_SENT_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> TOTAL_BYTES_RECEIVED
            = new (CATEGORY, TOTAL_BYTES_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_SEND
            = new (CATEGORY, LIVEKIT_ISLAND_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_ISLAND_RECEIVED
            = new (CATEGORY, LIVEKIT_ISLAND_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_SEND
            = new (CATEGORY, LIVEKIT_SCENE_SEND_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);

        public static readonly ProfilerCounterValue<ulong> LIVEKIT_SCENE_RECEIVED
            = new (CATEGORY, LIVEKIT_SCENE_RECEIVED_NAME, ProfilerMarkerDataUnit.Bytes, ProfilerCounterOptions.FlushOnEndOfFrame);
    }
}
#endif
