using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Thumbnails.Utils
{
    public static class LoadThumbnailsUtils
    {
        internal static readonly SpriteData DEFAULT_THUMBNAIL = new (new Texture2DData(Texture2D.grayTexture), Sprite.Create(Texture2D.grayTexture!, new Rect(0, 0, 1, 1), new Vector2()));
        private static readonly IExtendedObjectPool<URLBuilder> URL_BUILDER_POOL = new ExtendedObjectPool<URLBuilder>(() => new URLBuilder(), defaultCapacity: 2);

        public static async UniTask<Sprite> WaitForThumbnailAsync(this IAvatarAttachment avatarAttachment, int checkInterval, CancellationToken ct)
        {
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (avatarAttachment.ThumbnailAssetResult is not { IsInitialized: true });

            return avatarAttachment.ThumbnailAssetResult.Value.Asset;
        }

        public static async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(IWebRequestController webRequestController, URLDomain assetBundleURL,
            string hash, ReportData reportCategory, CancellationToken ct)
        {
            using var scope = URL_BUILDER_POOL.Get(out var urlBuilder);
            urlBuilder!.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            var sceneAbDto = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build(), attemptsCount: 1), ct, reportCategory)
                                                       .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);

            return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.Files, hash, sceneAbDto.Date);
        }

        private static async UniTask<bool> TryResolveAssetBundleManifestAsync(IWebRequestController requestController, URLDomain assetBundleURL, IAvatarAttachment attachment, CancellationTokenSource? cancellationTokenSource)
        {
            if (attachment.ManifestResult?.Asset == null)
                try
                {
                    var asset = await LoadAssetBundleManifestAsync(requestController, assetBundleURL, attachment.DTO.GetHash(), ReportCategory.WEARABLE, cancellationTokenSource?.Token ?? CancellationToken.None);
                    attachment.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>(asset);
                }
                catch (Exception) { return false; }

            return true;
        }

        private static void CreateWearableThumbnailTexturePromise(
            IRealmData realmData,
            URLPath thumbnailPath,
            IAvatarAttachment attachment,
            World world,
            IPartitionComponent partitionComponent,
            CancellationTokenSource? cancellationTokenSource = null
        )
        {
            using var urlBuilderScope = URL_BUILDER_POOL.AutoScope();
            var urlBuilder = urlBuilderScope.Value;
            urlBuilder.Clear();
            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(thumbnailPath);

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    // If cancellation token source was not provided a new one will be created
                    CommonArguments = new CommonLoadingArguments(urlBuilder.Build(), cancellationTokenSource: cancellationTokenSource),
                },
                partitionComponent);

            world.Create(attachment, promise, partitionComponent);
        }

        public static async UniTask CreateWearableThumbnailABPromiseAsync(
            IWebRequestController requestController,
            URLDomain assetBundleURL,
            IRealmData realmData,
            IAvatarAttachment attachment,
            World world,
            IPartitionComponent partitionComponent,
            CancellationTokenSource? cancellationTokenSource = null
        )
        {
            if (attachment.ThumbnailAssetResult != null)
                return;

            // it's a signal that the promise is already created, similar to `WearableAssets`
            attachment.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback();

            URLPath thumbnailPath = attachment.GetThumbnail();

            if (thumbnailPath.IsEmpty())
            {
                attachment.ThumbnailAssetResult = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_THUMBNAIL);
                return;
            }

            if (!await TryResolveAssetBundleManifestAsync(requestController, assetBundleURL, attachment, cancellationTokenSource))
            {
                ReportHub.Log(
                    ReportCategory.THUMBNAILS,
                    $"Cannot load the thumbnail of the wearable {attachment.GetUrn()} {attachment.DTO.GetHash()} since it doesnt have an AB manifest. " +
                    "Trying to get the texture through content server"
                );

                CreateWearableThumbnailTexturePromise(realmData, thumbnailPath, attachment, world, partitionComponent, cancellationTokenSource);
                return;
            }

            var promise = AssetBundlePromise.Create(
                world,
                GetAssetBundleIntention.FromHash(
                    typeof(Texture2D),
                    hash: thumbnailPath.Value + PlatformUtils.GetCurrentPlatform(),
                    permittedSources: AssetSource.ALL,
                    manifest: attachment.ManifestResult?.Asset,
                    cancellationTokenSource: cancellationTokenSource ?? new CancellationTokenSource()
                ),
                partitionComponent);

            world.Create(attachment, promise, partitionComponent);
        }
    }
}
