using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarAttachment<WearableDTO>, ITrimmedWearable
    {
        WearableType Type { get; }

        /// <summary>
        ///     Per <see cref="BodyShape" /> [MALE, FEMALE]
        /// </summary>
        WearableAssets[] WearableAssetResults { get; }

        /// <summary>
        ///     DTO must be resolved only one
        /// </summary>
        bool TryResolveDTO(StreamableLoadingResult<WearableDTO> result);

        bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash);

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        new bool IsCompatibleWithBodyShape(string bodyShape);

        bool HasSameModelsForAllGenders();

        bool IsOutlineCompatible();

        public static IWearable NewEmpty() =>
            new Wearable();

        // Resolve ambiguity: IWearable inherits IThumbnailAttachment through both IAvatarAttachment and ITrimmedAvatarAttachment
        // Use IAvatarAttachment implementation as the most specific one by implementing directly using DTO
        URLPath IThumbnailAttachment.GetThumbnail()
        {
            const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
            string thumbnailHash = this.DTO.Metadata.thumbnail;

            if (thumbnailHash != THUMBNAIL_DEFAULT_KEY || this.DTO.content == null) return new URLPath(thumbnailHash!);

            for (var i = 0; i < this.DTO.content.Length; i++)
                if (this.DTO.content[i].file == THUMBNAIL_DEFAULT_KEY)
                {
                    thumbnailHash = this.DTO.content[i].hash;
                    break;
                }

            return new URLPath(thumbnailHash);
        }

        URN IThumbnailAttachment.GetUrn() => this.DTO.Metadata.id;
        string IThumbnailAttachment.GetHash() => this.DTO.GetHash();
        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() => this.DTO.assetBundleManifestVersion;
        string? IThumbnailAttachment.GetContentDownloadUrl() => this.DTO.ContentDownloadUrl;
        string? IThumbnailAttachment.GetEntityId() => this.DTO.id;

        UniTask<Sprite> IThumbnailAttachment.WaitForThumbnailAsync(int checkInterval, CancellationToken ct) =>

            // This is set in ResolveAvatarAttachmentThumbnailSystem after being loaded by LoadAssetBundleSystem
            WaitForThumbnailAsyncImpl(this, checkInterval, ct);

        private static async UniTask<Sprite> WaitForThumbnailAsyncImpl(IThumbnailAttachment attachment, int checkInterval, CancellationToken ct)
        {
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (attachment.ThumbnailAssetResult is not { IsInitialized: true });

            return attachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
