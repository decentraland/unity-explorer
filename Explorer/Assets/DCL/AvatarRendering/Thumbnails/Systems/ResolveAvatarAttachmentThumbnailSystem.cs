using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Thumbnails.Systems
{
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
                return;

            if (promise.TryConsume(World, out var result))
            {
                var sprite = result.Asset?.GetMainAsset<Texture2D>();

                wearable.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(
                    result.Succeeded
                        ? Sprite.Create(sprite, new Rect(0, 0, sprite.width, sprite.height),
                            VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false)
                        : LoadThumbnailsUtils.DEFAULT_THUMBNAIL);

                World.Destroy(entity);
            }
        }

        [Query]
        private void CompleteWearableThumbnailDownload(Entity entity, ref IAvatarAttachment wearable, ref Promise promise)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
                return;

            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2D> result))
            {
                wearable.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(
                    result.Succeeded
                        ? Sprite.Create(result.Asset, new Rect(0, 0, result.Asset!.width, result.Asset.height),
                            VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false)
                        : LoadThumbnailsUtils.DEFAULT_THUMBNAIL);

                World.Destroy(entity);
            }
        }
    }
}
