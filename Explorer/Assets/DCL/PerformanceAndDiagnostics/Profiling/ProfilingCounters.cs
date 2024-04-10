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

        //LODS

        public static ProfilerCounterValue<int> LODAssetAmount =
            new(MEMORY, "LOD Assets", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> LODInstantiatedInCache =
            new(MEMORY, "LOD Assets In Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> LOD_0_Amount =
            new(MEMORY, "LOD 0 Amount Active", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> LOD_1_Amount =
            new(MEMORY, "LOD 1 Amount Active", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> LOD_2_Amount =
            new(MEMORY, "LOD 2 Amount Active", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> LOD_3_Amount =
            new(MEMORY, "LOD 3 Amount Active", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> Failling_LOD_Amount =
            new(MEMORY, "LOD Failed Amount", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int>[] LOD_Per_Level_Values =
        {
            LOD_0_Amount, LOD_1_Amount, LOD_2_Amount, LOD_3_Amount
        };
        
        
        // Textures cache
        public static ProfilerCounterValue<int> TexturesAmount =
            new (MEMORY, "Textures", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> TexturesInCache =
            new (MEMORY, "Textures In Cache", ProfilerMarkerDataUnit.Count);

        // AudioClips cache
        public static ProfilerCounterValue<int> AudioClipsAmount =
            new (MEMORY, "AudioClips", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> AudioClipsInCache =
            new (MEMORY, "AudioClips In Cache", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> AudioClipsReferenced =
            new (MEMORY, "AudioClips Referenced", ProfilerMarkerDataUnit.Count);

        // Profiles
        public static ProfilerCounterValue<int> ProfileIntentionsInCache =
            new (MEMORY, "Profile Intentions In Cache", ProfilerMarkerDataUnit.Count);

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

            LODInstantiatedInCache.Value = 0;
            LODAssetAmount.Value = 0;
            LOD_0_Amount.Value = 0;
            LOD_1_Amount.Value = 0;
            LOD_2_Amount.Value = 0;
            Failling_LOD_Amount.Value = 0;
            
            TexturesAmount.Value = 0;
            TexturesInCache.Value = 0;

            AudioClipsAmount.Value = 0;
            AudioClipsInCache.Value = 0;
            AudioClipsReferenced.Value = 0;

            ProfileIntentionsInCache.Value = 0;
        }
    }
}
