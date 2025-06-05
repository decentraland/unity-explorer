using System;

namespace ECS.StreamableLoading.Cache
{
    public interface ISizedStreamableCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention>, ISizedContent where TLoadingIntention: IEquatable<TLoadingIntention>
    {
        int ItemCount { get; }
    }
}
