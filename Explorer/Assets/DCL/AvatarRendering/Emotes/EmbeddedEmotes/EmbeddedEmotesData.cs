using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public struct EmbeddedEmote
    {
        public string id;
        public string name;
        public AudioClip audioClip;
        public Sprite thumbnail;

        // Represents unisex prefab
        public GameObject prefab;
        public EmoteDTO.EmoteMetadataDto.Data entity;
    }

    [CreateAssetMenu(menuName = "DCL/Avatar/Embedded Emotes Data")]
    public class EmbeddedEmotesData : ScriptableObject
    {
        public EmbeddedEmote[] emotes;

        private List<IEmote>? generatedEmotes;

        public IEnumerable<IEmote> GenerateEmotes()
        {
            if (generatedEmotes != null) return generatedEmotes;

            generatedEmotes = new List<IEmote>();

            foreach (EmbeddedEmote embeddedEmote in emotes)
            {
                var model = new EmoteDTO();
                var emote = new Emote(new StreamableLoadingResult<EmoteDTO>(), false);
                model.id = embeddedEmote.id;

                // No content hashes available
                model.content = Array.Empty<ContentDefinition>();
                model.pointers = new[] { embeddedEmote.id };
                model.type = "emote";
                model.version = "v3";
                model.metadata = new EmoteDTO.EmoteMetadataDto();
                model.metadata.id = embeddedEmote.id;
                model.metadata.name = embeddedEmote.name;

                model.metadata.i18n = new[]
                {
                    new AvatarAttachmentDTO.I18n
                    {
                        code = "en",
                        text = embeddedEmote.name,
                    },
                };

                model.metadata.thumbnail = "thumbnail";
                model.metadata.emoteDataADR74 = embeddedEmote.entity;

                emote.Model = new StreamableLoadingResult<EmoteDTO>(model);
                emote.ThumbnailAssetResult = embeddedEmote.thumbnail.ToUnownedSpriteData();


                AttachmentRegularAsset unisexAsset = CreateAttachmentAsset(embeddedEmote.prefab);
                unisexAsset.AddReference();
                var unisexAssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(unisexAsset);
                emote.AssetResult = unisexAssetResult;

                emote.AudioAssetResult = new StreamableLoadingResult<AudioClipData>(new AudioClipData(embeddedEmote.audioClip));

                emote.ManifestResult = null;

                generatedEmotes.Add(emote);
            }

            return generatedEmotes;
        }

        private static AttachmentRegularAsset CreateAttachmentAsset(GameObject glb)
        {
            var rendererInfos = new List<AttachmentRegularAsset.RendererInfo>();

            foreach (SkinnedMeshRenderer? renderer in glb.GetComponentsInChildren<SkinnedMeshRenderer>())
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(renderer.sharedMaterial));

            return new AttachmentRegularAsset(glb, rendererInfos, IStreamableRefCountData.Null.INSTANCE);
        }
    }
}
