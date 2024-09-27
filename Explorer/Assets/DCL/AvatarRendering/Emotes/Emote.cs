using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class Emote : IEmote
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; } = new StreamableLoadingResult<AttachmentRegularAsset>?[BodyShape.COUNT];
        public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }
        public StreamableLoadingResult<EmoteDTO> Model { get; set; }
        public StreamableLoadingResult<AudioClip>?[] AudioAssetResults { get; set; } = new StreamableLoadingResult<AudioClip>?[BodyShape.COUNT];

        public bool IsLoading { get; private set; }

        public Emote(StreamableLoadingResult<EmoteDTO> model, bool isLoading = true)
        {
            Model = model;
            IsLoading = isLoading;
        }

        public void UpdateLoadingStatus(bool isLoading)
        {
            IsLoading = isLoading;
        }

        public bool IsOnChain() =>
            IsOnChain(id: ((IAvatarAttachment<EmoteDTO>)this).GetUrn().ToString());

        public static bool IsOnChain(string id) =>
            id.StartsWith("urn:") && !id.StartsWith("urn:decentraland:off-chain:");

        public AvatarAttachmentDTO DTO =>
            Model.Asset!;

        public override string ToString() =>
            ((IAvatarAttachment<EmoteDTO>)this).ToString();

        public bool IsLooping() =>
            Model.Asset.metadata.emoteDataADR74.loop;

        public bool HasSameClipForAllGenders()
        {
            IAvatarAttachment attachment = this;

            attachment.TryGetMainFileHash(BodyShape.MALE, out string? maleHash);
            attachment.TryGetMainFileHash(BodyShape.FEMALE, out string? femaleHash);

            return maleHash == femaleHash;
        }
    }
}
