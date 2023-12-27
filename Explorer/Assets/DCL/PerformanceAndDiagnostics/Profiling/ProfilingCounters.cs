using Unity.Profiling;

namespace DCL.Profiling
{
    public static class
        ProfilingCounters
    {
        private static readonly ProfilerCategory MEMORY = ProfilerCategory.Memory;

        // Asset Bundle cache
        public static ProfilerCounterValue<int> ABDataAmount =
            new (MEMORY, "AB Data Amount", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> AssetBundlesInCache =
            new (MEMORY, "AB Data in Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> ABReferencedAmount =
            new (MEMORY, "AB Referenced Amount", ProfilerMarkerDataUnit.Count);

        // GLTF Container cache
        public static ProfilerCounterValue<int> GltfContainerAssetsAmount =
            new (MEMORY, "GLTF ContainerAssets", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> GltfInCacheAmount =
            new (MEMORY, "GLTF Assets in Cache", ProfilerMarkerDataUnit.Count);

        // Wearables cache
        public static ProfilerCounterValue<int> WearablesAssetsAmount =
            new (MEMORY, "Wearables Assets", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> EmptyWearablesAssetsAmount =
            new (MEMORY, "Empty Wearables Assets", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> WearablesAssetsReferencedAmount =
            new (MEMORY, "Referenced Wearables Assets", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> WearablesAssetsInCacheAmount =
            new (MEMORY, "Wearables Assets In Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> WearablesAssetsInCatalogAmount =
            new (MEMORY, "Wearables Assets In Catalog", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> CachedWearablesAmount =
            new (MEMORY, "Cached Wearables", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> CachedWearablesInCacheAmount =
            new (MEMORY, "Cached Wearables In Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> GetWearablesIntentionAmount =
            new (MEMORY, "GetWearables Intentions", ProfilerMarkerDataUnit.Count);

        // Textures cache
        public static ProfilerCounterValue<int> TexturesAmount =
            new (MEMORY, "Textures", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> TexturesInCache =
            new (MEMORY, "Textures In Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> ProfilesInCache =
            new (MEMORY, "Profiles In Cache", ProfilerMarkerDataUnit.Count);

        public static void CleanAllCounters()
        {
            ABDataAmount.Value = 0;
            ABReferencedAmount.Value = 0;

            GltfContainerAssetsAmount.Value = 0;
            GltfInCacheAmount.Value = 0;

            GetWearablesIntentionAmount.Value = 0;
            WearablesAssetsAmount.Value = 0;
            EmptyWearablesAssetsAmount.Value = 0;
            WearablesAssetsReferencedAmount.Value = 0;
            WearablesAssetsInCacheAmount.Value = 0;
            WearablesAssetsInCatalogAmount.Value = 0;

            CachedWearablesAmount.Value = 0;
            CachedWearablesInCacheAmount.Value = 0;

            TexturesAmount.Value = 0;
            TexturesInCache.Value = 0;

            ProfilesInCache.Value = 0;
        }
    }
}
