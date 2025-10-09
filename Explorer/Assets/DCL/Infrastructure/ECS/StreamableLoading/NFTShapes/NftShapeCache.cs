using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;

namespace ECS.StreamableLoading.NFTShapes
{
    public class NftShapeCache : ISizedStreamableCache<Texture2DData, GetNFTImageIntention>,
        ISizedStreamableCache<Texture2DData, GetNFTVideoIntention>
    {
        private readonly TexturesCache<GetNFTImageIntention> imageCache;
        private readonly TexturesCache<GetNFTVideoIntention> videoCache;

        IDictionary<IntentionsComparer<GetNFTImageIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<Texture2DData>>> IStreamableCache<Texture2DData, GetNFTImageIntention>.OngoingRequests => imageCache.OngoingRequests;

        IDictionary<IntentionsComparer<GetNFTVideoIntention>.SourcedIntentionId, StreamableLoadingResult<Texture2DData>?> IStreamableCache<Texture2DData, GetNFTVideoIntention>.IrrecoverableFailures => videoCache.IrrecoverableFailures;

        IDictionary<IntentionsComparer<GetNFTVideoIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<Texture2DData>>> IStreamableCache<Texture2DData, GetNFTVideoIntention>.OngoingRequests => videoCache.OngoingRequests;

        IDictionary<IntentionsComparer<GetNFTImageIntention>.SourcedIntentionId, StreamableLoadingResult<Texture2DData>?> IStreamableCache<Texture2DData, GetNFTImageIntention>.IrrecoverableFailures => imageCache.IrrecoverableFailures;

        public long ByteSize => imageCache.ByteSize + videoCache.ByteSize;
        public int ItemCount => imageCache.ItemCount + videoCache.ItemCount;

        public NftShapeCache(TexturesCache<GetNFTImageIntention> imageCache,
            TexturesCache<GetNFTVideoIntention> videoCache)
        {
            this.imageCache = imageCache;
            this.videoCache = videoCache;
        }

        public void Dispose()
        {
            imageCache.Dispose();
            videoCache.Dispose();
        }

        public bool TryGet(in GetNFTVideoIntention key, out Texture2DData asset) =>
            videoCache.TryGet(key, out asset);

        public void Add(in GetNFTVideoIntention key, Texture2DData asset) =>
            videoCache.Add(key, asset);

        public bool TryGet(in GetNFTImageIntention key, out Texture2DData asset) =>
            imageCache.TryGet(key, out asset);

        public void Add(in GetNFTImageIntention key, Texture2DData asset) =>
            imageCache.Add(key, asset);

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            imageCache.Unload(frameTimeBudget, maxUnloadAmount);
            videoCache.Unload(frameTimeBudget, maxUnloadAmount);
        }
    }
}
