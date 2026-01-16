using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.Components
{
    /// <summary>
    ///     Common interface for types that can provide thumbnails.
    ///     Provides all necessary data and methods to create promises and wait for thumbnails.
    /// </summary>
    public interface IThumbnailAttachment
    {
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        /// <summary>
        ///     Gets the thumbnail path
        /// </summary>
        URLPath GetThumbnail();

        /// <summary>
        ///     Gets the URN of the attachment
        /// </summary>
        URN GetUrn();

        /// <summary>
        ///     Gets the hash/ID of the attachment
        /// </summary>
        string GetHash();

        /// <summary>
        ///     Gets the asset bundle manifest version
        /// </summary>
        AssetBundleManifestVersion? GetAssetBundleManifestVersion();

        /// <summary>
        ///     Gets the content download URL
        /// </summary>
        string? GetContentDownloadUrl();

        /// <summary>
        ///     Gets the entity ID for the promise
        /// </summary>
        string? GetEntityId();

        /// <summary>
        ///     Waits for the thumbnail to be loaded
        /// </summary>
        async UniTask<Sprite> WaitForThumbnailAsync(int checkInterval, CancellationToken ct)
        {
            // This is set in ResolveAvatarAttachmentThumbnailSystem after being loaded by LoadAssetBundleSystem
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (ThumbnailAssetResult is not { IsInitialized: true });

            return ThumbnailAssetResult!.Value.Asset;
        }
    }
}