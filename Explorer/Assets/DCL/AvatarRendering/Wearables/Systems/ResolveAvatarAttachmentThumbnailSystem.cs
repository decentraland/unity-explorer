using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveAvatarAttachmentThumbnailSystem : BaseUnityLoopSystem
    {
        public ResolveAvatarAttachmentThumbnailSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            CompleteWearableThumbnailDownloadQuery(World);
        }

        [Query]
        private void CompleteWearableThumbnailDownload(in Entity entity, ref IAvatarAttachment wearable, ref Promise promise)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2D> result))
            {
                wearable.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(
                    result.Succeeded
                        ? Sprite.Create(result.Asset, new Rect(0, 0, result.Asset.width, result.Asset.height),
                            VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false)
                        : WearableComponentsUtils.DEFAULT_THUMBNAIL);

                World.Destroy(entity);
            }
        }
    }
}
