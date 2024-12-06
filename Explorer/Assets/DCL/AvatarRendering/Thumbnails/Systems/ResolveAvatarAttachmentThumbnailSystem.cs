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
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;
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
        private void CompleteWearableABThumbnailDownload(Entity entity, ref IAvatarAttachment wearable, ref AssetBundlePromise promise)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                wearable.ThumbnailAssetResult = null;
                return;
            }


            if (promise.TryConsume(World, out var result))
            {
                wearable.ThumbnailAssetResult = result.ToFullRectSpriteData(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
                World.Destroy(entity);
            }
        }

        [Query]
        private void CompleteWearableThumbnailDownload(Entity entity, ref IAvatarAttachment wearable, ref Promise promise)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                wearable.ThumbnailAssetResult = null;
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2DData> result))
            {
                wearable.ThumbnailAssetResult = result.ToFullRectSpriteData(LoadThumbnailsUtils.DEFAULT_THUMBNAIL);
                World.Destroy(entity);
            }
        }
    }
}
