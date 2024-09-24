using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.WebRequests;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;
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

        public async UniTask<Sprite> GetAsync(IAvatarAttachment avatarAttachment, CancellationToken ct)
        {
            if (avatarAttachment.ThumbnailAssetResult is { IsInitialized: true })
                return avatarAttachment.ThumbnailAssetResult.Value.Asset;

            bool promiseAlreadyCreated = avatarAttachment.ThumbnailAssetResult != null;

            // Create a new promise bound to the current cancellation token
            // if the promise was created before, we should not override its cancellation
            if (!promiseAlreadyCreated)
            {
                // it's a signal that the promise is already created, similar to `WearableAssets`
                avatarAttachment.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback();

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
            return await avatarAttachment.WaitForThumbnailAsync(0, ct);
        }
    }
}
