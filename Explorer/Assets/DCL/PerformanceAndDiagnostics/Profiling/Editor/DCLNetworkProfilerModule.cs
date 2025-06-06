using System;
using Unity.Profiling.Editor;
using static DCL.Profiling.NetworkProfilerCounters;

namespace DCL.Profiling.Editor
{
    [Serializable, ProfilerModuleMetadata(CATEGORY_NAME)]
    public sealed class DCLNetworkProfilerModule: ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] CHART_COUNTERS =
        {
            new(WIFI_IPV4_MBPS_SENT_NAME, CATEGORY),
            new(WIFI_IPV4_MBPS_RECEIVED_NAME, CATEGORY),

            new(WIFI_IPV4_BYTES_SENT_FRAME_NAME, CATEGORY),
            new(WIFI_IPV4_BYTES_RECEIVED_FRAME_NAME, CATEGORY),

            new(LIVEKIT_ISLAND_SEND_NAME, CATEGORY),
            new(LIVEKIT_SCENE_SEND_NAME, CATEGORY),
            new(LIVEKIT_ISLAND_RECEIVED_NAME, CATEGORY),
            new(LIVEKIT_SCENE_RECEIVED_NAME, CATEGORY),

            new(WEB_REQUESTS_UPLOADED_FRAME_NAME, CATEGORY),
            new(WEB_REQUESTS_DOWNLOADED_FRAME_NAME, CATEGORY),
        };

        public DCLNetworkProfilerModule() : base(CHART_COUNTERS) { }
    }
}
