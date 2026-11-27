using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>, ITrimmedEmote
    {
        new int Amount { get; set; }

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

        void PopulateTrimmedModel()
        {
            if (Model.IsInitialized)
                TrimmedModel = new StreamableLoadingResult<TrimmedEmoteDTO>(Model.Asset!.Convert(((IAvatarAttachment)this).GetThumbnail().Value));
        }
    }
}
