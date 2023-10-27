using Unity.Profiling;

namespace ECS.StreamableLoading.Cache
{
    public class GameStats
    {
        // Asset Bundle cache
        private static readonly ProfilerCategory MyProfilerCategory = ProfilerCategory.Memory;

        public static ProfilerCounterValue<int> ABCacheSizeCounter =
            new (MyProfilerCategory, "AB Cache Size", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> GLTFCacheSizeCounter =
            new (MyProfilerCategory, "GLTF Cache Size", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> WearablesCacheSizeCounter =
            new (MyProfilerCategory, "Wearables Cache Size", ProfilerMarkerDataUnit.Count);
    }
}
