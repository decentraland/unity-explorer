using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Loading.Assets
{
    public interface IAttachmentsAssetsCache
    {
        int AssetsCount { get; }

        bool TryGet(AttachmentAssetBase asset, out CachedAttachment instance);

        void Release(CachedAttachment cachedAttachment);

        void ReleaseSpringBones(System.Collections.Generic.IList<CachedAttachment> wearables);

        void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount);
    }
}
