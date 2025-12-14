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
    // It implements ITrimmedWearable to allow using trimmed wearables in systems that only need trimmed data, such as the builder flow. Check LoadWearablesByParamSystem and ApplicationParametersWearablesProvider
    public interface IWearable : IAvatarAttachment<WearableDTO>, ITrimmedWearable
    {
        int Amount { get; }
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
            string thumbnailHash = DTO.Metadata.thumbnail;

            if (thumbnailHash != THUMBNAIL_DEFAULT_KEY || DTO.content == null) return new URLPath(thumbnailHash!);

            for (int i = 0; i < DTO.content.Length; i++)
                if (DTO.content[i].file == THUMBNAIL_DEFAULT_KEY)
                {
                    thumbnailHash = DTO.content[i].hash;
                    break;
                }

            return new URLPath(thumbnailHash);
        }

        URN IThumbnailAttachment.GetUrn()
        {
            return DTO.Metadata.id;
        }

        string IThumbnailAttachment.GetHash()
        {
            return DTO.GetHash();
        }

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion()
        {
            return DTO.assetBundleManifestVersion;
        }

        string? IThumbnailAttachment.GetContentDownloadUrl()
        {
            return DTO.ContentDownloadUrl;
        }

        string? IThumbnailAttachment.GetEntityId()
        {
            return DTO.id;
        }

        UniTask<Sprite> IThumbnailAttachment.WaitForThumbnailAsync(int checkInterval, CancellationToken ct)
        {
            // This is set in ResolveAvatarAttachmentThumbnailSystem after being loaded by LoadAssetBundleSystem
            return WaitForThumbnailImplAsync(this, checkInterval, ct);
        }

        private static async UniTask<Sprite> WaitForThumbnailImplAsync(IThumbnailAttachment attachment, int checkInterval, CancellationToken ct)
        {
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (attachment.ThumbnailAssetResult is not { IsInitialized: true });

            return attachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
