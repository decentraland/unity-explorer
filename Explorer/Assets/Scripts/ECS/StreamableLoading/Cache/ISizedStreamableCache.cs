namespace ECS.StreamableLoading.Cache
{
    public interface ISizedStreamableCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention>, ISizedContent
    {
        int ItemCount { get; }
    }
}
