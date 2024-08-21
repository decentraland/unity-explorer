using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class Emote : IEmote
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<WearableRegularAsset>?[] AssetResults { get; } = new StreamableLoadingResult<WearableRegularAsset>?[BodyShape.COUNT];
        public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }
        public StreamableLoadingResult<EmoteDTO> Model { get; set; }
        public StreamableLoadingResult<AudioClip>?[] AudioAssetResults { get; set; } = new StreamableLoadingResult<AudioClip>?[BodyShape.COUNT];

        public bool IsLoading { get; set; } = true;

        public bool IsOnChain() =>
            IsOnChain(id: ((IAvatarAttachment) this).GetUrn().ToString());

        public static bool IsOnChain(string id) =>
            id.StartsWith("urn:") && !id.StartsWith("urn:decentraland:off-chain:");

        public AvatarAttachmentDTO GetDTO() =>
            Model.Asset!;

        public override string ToString() =>
            ((IAvatarAttachment)this).ToString();

        public bool IsLooping() =>
            Model.Asset.metadata.emoteDataADR74.loop;
    }
}
