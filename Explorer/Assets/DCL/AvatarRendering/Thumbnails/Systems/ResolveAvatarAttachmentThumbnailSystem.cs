using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.AssetBundles;
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
                // Stamp Cancelled only while the slot is in-flight; an initialized slot (e.g. Failed from a consumer timeout) already carries the terminal state
                if (wearable.ThumbnailAssetResult is not { IsInitialized: true })
                    wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.CancelledResult();
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                wearable.ThumbnailAssetResult = ToThumbnail(result);
                World.Destroy(entity);
            }
        }

        [Query]
        private void CompleteWearableThumbnailDownload(Entity entity, ref IThumbnailAttachment wearable, ref Promise promise)
        {
            if (promise.IsCancellationRequested(World))
            {
                // Stamp Cancelled only while the slot is in-flight; an initialized slot already carries the terminal state
                if (wearable.ThumbnailAssetResult is not { IsInitialized: true })
                    wearable.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.CancelledResult();
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
            {
                wearable.ThumbnailAssetResult = result.Succeeded ? result.ToFullRectSpriteData(LoadThumbnailsUtils.DEFAULT_THUMBNAIL) : FailedDownload();
                World.Destroy(entity);
            }
        }

        /// <summary>
        ///     A bundle that loaded without a texture is a failed download too. No sprite keeps the reference its load took, so it is released here.
        /// </summary>
        private static StreamableLoadingResult<SpriteData>.WithFallback ToThumbnail(in StreamableLoadingResult<AssetBundleData> result)
        {
            if (!result.Succeeded)
                return FailedDownload();

            if (result.Asset!.TryToFullRectSpriteData(out SpriteData spriteData))
                return new StreamableLoadingResult<SpriteData>.WithFallback(spriteData);

            result.Asset.Dereference();
            return FailedDownload();
        }

        /// <summary>
        ///     The result of a failed download: not a success, carrying the default thumbnail to show in its place.
        /// </summary>
        private static StreamableLoadingResult<SpriteData>.WithFallback FailedDownload() =>
            StreamableLoadingResult<SpriteData>.WithFallback.Failed(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
    }
}
