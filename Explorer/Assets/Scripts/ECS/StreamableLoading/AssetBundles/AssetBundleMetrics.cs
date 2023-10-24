using System;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public struct AssetBundleMetrics
    {
        public long estimatedSizeInMB;
        public long meshesEstimatedSize;
        public long animationsEstimatedSize;
    }
}
