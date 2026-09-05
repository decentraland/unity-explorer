using System;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public struct AssetBundleMetrics
    {
        public long meshesEstimatedSize;
        public long animationsEstimatedSize;
    }
}
