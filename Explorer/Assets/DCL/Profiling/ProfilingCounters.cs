using Unity.Profiling;

namespace DCL.Profiling
{
    public static class ProfilingCounters
    {
        private static readonly ProfilerCategory MEMORY = ProfilerCategory.Memory;

        // Asset Bundle cache
        public static ProfilerCounterValue<int> ABDataAmount =
            new (MEMORY, "AB Data Amount", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> ABCacheSize =
            new (MEMORY, "AB Cache Size", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> ABReferencedAmount =
            new (MEMORY, "AB Referenced Amount", ProfilerMarkerDataUnit.Count);

        // GLTF Container cache
        public static ProfilerCounterValue<int> GLTFContainerAssetsAmount =
            new (MEMORY, "GLTF ContainerAssets Amount", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> GLTFCacheSize =
            new (MEMORY, "GLTF Cache Size", ProfilerMarkerDataUnit.Count);

        // Wearables cache
        public static ProfilerCounterValue<int> WearablesAssetsAmount =
            new (MEMORY, "Wearables Assets Amount", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> WearablesAmountCacheSize =
            new (MEMORY, "Wearables Cache Size", ProfilerMarkerDataUnit.Count);

        public static void CleanAllCounters()
        {
            ABDataAmount.Value = 0;
            ABCacheSize.Value = 0;
            ABReferencedAmount.Value = 0;

            GLTFContainerAssetsAmount.Value = 0;
            GLTFCacheSize.Value = 0;

            WearablesAssetsAmount.Value = 0;
            WearablesAmountCacheSize.Value = 0;
        }
    }
}
