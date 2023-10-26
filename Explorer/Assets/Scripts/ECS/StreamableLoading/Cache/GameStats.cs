using Unity.Profiling;

namespace ECS.StreamableLoading.Cache
{
    public class GameStats
    {
        private static readonly ProfilerCategory MyProfilerCategory = ProfilerCategory.Memory;
        public static readonly ProfilerCounter<int> ABCacheChangeCalls =
            new (MyProfilerCategory, "AB Cache Size Calls", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> ABCacheSizeCounter =
            new (MyProfilerCategory, "AB Cache Size", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> GLTFCacheSizeCounter =
            new (MyProfilerCategory, "GLTF Cache Size", ProfilerMarkerDataUnit.Count);

        // ---------------
    }
}
