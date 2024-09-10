using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility;
using ThumbnailPromise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSThumbnailProvider : IThumbnailProvider
    {
        private readonly IRealmData realmData;
        private readonly World world;
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController requestController;

        public ECSThumbnailProvider(IRealmData realmData,
            World world, URLDomain assetBundleURL, IWebRequestController requestController)
        {
            this.realmData = realmData;
            this.world = world;
            this.assetBundleURL = assetBundleURL;
            this.requestController = requestController;
        }

        public async UniTask<Sprite?> GetAsync(IAvatarAttachment avatarAttachment, CancellationToken ct)
        {
            if (avatarAttachment.ThumbnailAssetResult != null)
                return avatarAttachment.ThumbnailAssetResult.Value.Asset;

            bool promiseAlreadyCreated = false;

            world.Query(in new QueryDescription().WithAll<IAvatarAttachment, AssetBundlePromise, IPartitionComponent>(),
                (ref IAvatarAttachment attachment, ref AssetBundlePromise promise) =>
                {
                    if (attachment.GetThumbnail().Equals(avatarAttachment.GetThumbnail()))
                        promiseAlreadyCreated = true;
                });

            // Create a new promise bound to the current cancellation token
            // if the promise was created before, we should not override its cancellation
            if (!promiseAlreadyCreated)
            {
                await LoadThumbnailsUtils.CreateWearableThumbnailABPromiseAsync(
                    requestController,
                    assetBundleURL,
                    realmData,
                    avatarAttachment,
                    world,
                    PartitionComponent.TOP_PRIORITY,
                    CancellationTokenSource.CreateLinkedTokenSource(ct));
            }

            // We dont create an async task from the promise since it needs to be consumed at the proper system, not here
            // The promise's result will eventually get replicated into the avatar attachment
            await UniTask.WaitWhile(() => avatarAttachment.ThumbnailAssetResult == null, cancellationToken: ct);

            return avatarAttachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
