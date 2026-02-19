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

        new void SetAmount(int amount);

        new string GetCategory() =>
            ((IAvatarAttachment)this).GetCategory();

        new string GetRarity() =>
            ((IAvatarAttachment)this).GetRarity();

        new string GetName() =>
            ((IAvatarAttachment)this).GetName();

        new URN GetUrn() =>
            ((IAvatarAttachment)this).GetUrn();

        URLPath IThumbnailAttachment.GetThumbnail() =>
            ((IAvatarAttachment)this).GetThumbnail();

        URN IThumbnailAttachment.GetUrn() =>
            ((IAvatarAttachment)this).GetUrn();

        string IThumbnailAttachment.GetHash() =>
            ((IAvatarAttachment)this).GetHash();

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() =>
            ((IAvatarAttachment)this).GetAssetBundleManifestVersion();

        string? IThumbnailAttachment.GetContentDownloadUrl() =>
            ((IAvatarAttachment)this).GetContentDownloadUrl();

        string? IThumbnailAttachment.GetEntityId() =>
            ((IAvatarAttachment)this).GetEntityId();

        UniTask<Sprite> IThumbnailAttachment.WaitForThumbnailAsync(int checkInterval, CancellationToken ct) =>
            WaitForThumbnailImplAsync(this, checkInterval, ct);

        private static async UniTask<Sprite> WaitForThumbnailImplAsync(IThumbnailAttachment attachment, int checkInterval, CancellationToken ct)
        {
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (attachment.ThumbnailAssetResult is not { IsInitialized: true });

            return attachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
