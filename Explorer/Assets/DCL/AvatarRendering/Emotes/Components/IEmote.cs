using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>, ITrimmedEmote
    {
        int Amount { get; set; }

        StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; }
        StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; }

        bool IsLooping();

        bool HasSameClipForAllGenders();

        public static IEmote NewEmpty() =>
            new Emote();

        // IThumbnailAttachment implementation
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

        URN IThumbnailAttachment.GetUrn() =>
            DTO.Metadata.id;

        string IThumbnailAttachment.GetHash() =>
            DTO.GetHash();

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() =>
            DTO.assetBundleManifestVersion;

        string? IThumbnailAttachment.GetContentDownloadUrl() =>
            DTO.ContentDownloadUrl;

        string? IThumbnailAttachment.GetEntityId() =>
            DTO.id;

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
