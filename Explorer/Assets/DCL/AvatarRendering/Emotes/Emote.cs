using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public class Emote : IEmote
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; } = new StreamableLoadingResult<AttachmentRegularAsset>?[BodyShape.COUNT];
        public bool IsSocial => ((EmoteDTO.EmoteMetadataDto)this.DTO.Metadata).IsSocialEmote;
        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public StreamableLoadingResult<EmoteDTO> Model { get; set; }
        public StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; } = new StreamableLoadingResult<AudioClipData>?[BodyShape.COUNT];
        public List<StreamableLoadingResult<AudioClipData>> SocialEmoteOutcomeAudioAssetResults { get; } = new ();

        public bool IsLoading { get; private set; }

        public Emote() { }

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
            IsOnChain(id: this.GetUrn().ToString());

        public static bool IsOnChain(string id) =>
            id.StartsWith("urn:") && !id.StartsWith("urn:decentraland:off-chain:");

        public AvatarAttachmentDTO DTO =>
            Model.Asset!;

        public override string ToString() =>
            ((IAvatarAttachment<EmoteDTO>)this).ToString();

        public bool IsLooping()
        {
            if (IsSocial)
            {
                // The Armature applies to the avatar that plays the start animation
                return Model.Asset is { metadata: { socialEmoteData: { startAnimation: { loop: true } } } };
            }
            else
            {
                //as the Asset is nullable the loop property might be retrieved in situations in which the Asset has not been yet loaded
                //to avoid a breaking null reference we provide safe access to the loop property by using the is pattern
                return Model.Asset is { metadata: { data: { loop: true } } };
            }
        }


        public bool HasSameClipForAllGenders()
        {
            IAvatarAttachment attachment = this;

            attachment.TryGetMainFileHash(BodyShape.MALE, out string? maleHash);
            attachment.TryGetMainFileHash(BodyShape.FEMALE, out string? femaleHash);

            return maleHash == femaleHash;
        }
    }
}
