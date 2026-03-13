using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Utility;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Thumbnails.Utils
{
    public static class LoadThumbnailsUtils
    {
        public static readonly SpriteData DEFAULT_THUMBNAIL = new (new TextureData(Texture2D.grayTexture), Sprite.Create(Texture2D.grayTexture!, new Rect(0, 0, 1, 1), new Vector2()));

        public static void CreateThumbnailABPromise(
            IDecentralandUrlsSource realmData,
            IThumbnailAttachment attachment,
            World world,
            IPartitionComponent partitionComponent,
            CancellationTokenSource? cancellationTokenSource = null
        )
        {
            // Means that it has already been resolved, or we have an un-cancelled promise running that we want to keep
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

            var assetBundleManifestVersion = attachment.GetAssetBundleManifestVersion();
            if (assetBundleManifestVersion != null && (assetBundleManifestVersion.IsLSDAsset || assetBundleManifestVersion.assetBundleManifestRequestFailed))
            {
                ReportHub.Log(
                    ReportCategory.THUMBNAILS,
                    $"Cannot load the thumbnail of the wearable {attachment.GetUrn()} {attachment.GetHash()} since it doesnt have an AB manifest. " +
                    "Trying to get the texture through content server"
                );

                CreateThumbnailTexturePromise(realmData, thumbnailPath, attachment, world, partitionComponent, cancellationTokenSource);
                return;
            }

            var promise = AssetBundlePromise.Create(
                world,
                GetAssetBundleIntention.FromHash(
                    hash: thumbnailPath.Value + PlatformUtils.GetCurrentPlatform(),
                    typeof(Texture2D),
                    permittedSources: AssetSource.ALL,
                    assetBundleManifestVersion: assetBundleManifestVersion,
                    parentEntityID: attachment.GetEntityId(),
                    cancellationTokenSource: cancellationTokenSource ?? new CancellationTokenSource()
                ),
                partitionComponent);

            world.Create(attachment, promise, partitionComponent);
        }

        private static void CreateThumbnailTexturePromise(
            IDecentralandUrlsSource urlsSource,
            URLPath thumbnailPath,
            IThumbnailAttachment attachment,
            World world,
            IPartitionComponent partitionComponent,
            CancellationTokenSource? cancellationTokenSource = null
        )
        {
            using PooledObject<URLBuilder> urlBuilderScope = DecentralandUrlsUtils.BuildFromDomain(attachment.GetContentDownloadUrl() ?? urlsSource.Url(DecentralandUrl.Content), out URLBuilder urlBuilder);
            urlBuilder.AppendPath(thumbnailPath);

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    // If cancellation token source was not provided a new one will be created
                    CommonArguments = new CommonLoadingArguments(urlBuilder.Build(), cancellationTokenSource: cancellationTokenSource), ReportSource = "AvatarRendering.LoadThumbnailsUtils"
                },
                partitionComponent);

            world.Create(attachment, promise, partitionComponent);
        }
    }

}
