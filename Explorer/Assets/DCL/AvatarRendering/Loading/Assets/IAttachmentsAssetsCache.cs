using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Loading.Assets
{
    public interface IAttachmentsAssetsCache
    {
        int AssetsCount { get; }

        bool TryGet(AttachmentAssetBase asset, out CachedAttachment instance);

        void Release(CachedAttachment cachedAttachment);

        void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount);
    }

    public sealed class AttachmentAssetsDontCache : IAttachmentsAssetsCache
    {
        public int AssetsCount => 0;

        public void Release(CachedAttachment cachedAttachment) { }

        public bool TryGet(AttachmentAssetBase asset, out CachedAttachment instance)
        {
            instance = default;
            return false;
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }
    }
}
