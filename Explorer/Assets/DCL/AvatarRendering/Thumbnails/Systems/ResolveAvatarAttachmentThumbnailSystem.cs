using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Thumbnails.Systems
{
    /// <summary>
    ///     TODO must check if the wearable is no longer in the cache, otherwise ref count leaks
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveAvatarAttachmentThumbnailSystem : BaseUnityLoopSystem
    {
        public ResolveAvatarAttachmentThumbnailSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            CompleteWearableThumbnailDownloadQuery(World);
            CompleteWearableABThumbnailDownloadQuery(World);
        }

        [Query]
        private void CompleteWearableABThumbnailDownload(Entity entity, ref IThumbnailAttachment wearable, ref AssetBundlePromise promise)
        {
            if (promise.IsCancellationRequested(World))
            {
                // Release waiters with Cancelled only while the slot still signals in-flight; an
                // initialized slot (e.g. Failed from a consumer timeout) already carries this
                // attempt's terminal state. Either way the next GetAsync clears it and retries.
                if (wearable.ThumbnailAssetResult is not { IsInitialized: true })
                    wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.CancelledResult();
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out var result))
            {
                wearable.ThumbnailAssetResult = result.ToFullRectSpriteData(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
                World.Destroy(entity);
            }
        }

        [Query]
        private void CompleteWearableThumbnailDownload(Entity entity, ref IThumbnailAttachment wearable, ref Promise promise)
        {
            if (promise.IsCancellationRequested(World))
            {
                // Release waiters with Cancelled only while the slot still signals in-flight; an
                // initialized slot already carries this attempt's terminal state.
                if (wearable.ThumbnailAssetResult is not { IsInitialized: true })
                    wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.CancelledResult();
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
            {
                wearable.ThumbnailAssetResult = result.ToFullRectSpriteData(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
                World.Destroy(entity);
            }
        }
    }
}
