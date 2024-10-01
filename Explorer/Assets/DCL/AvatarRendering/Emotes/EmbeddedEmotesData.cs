using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public GameObject male;
        public GameObject female;
        public EmoteDTO.Metadata.Data entity;
    }

    [CreateAssetMenu(menuName = "DCL/Emotes/EmbedEmotes")]
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
                model.content = Array.Empty<AvatarAttachmentDTO.Content>();
                model.pointers = new[] { embeddedEmote.id };
                model.type = "emote";
                model.version = "v3";
                model.metadata = new EmoteDTO.Metadata();
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

                if (embeddedEmote.male != null)
                {
                    AttachmentRegularAsset maleAsset = CreateAttachmentAsset(embeddedEmote.male);
                    maleAsset.AddReference();
                    var maleAssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(maleAsset);
                    emote.AssetResults[BodyShape.MALE] = maleAssetResult;
                }

                if (embeddedEmote.female != null)
                {
                    AttachmentRegularAsset femaleAsset = CreateAttachmentAsset(embeddedEmote.female);
                    femaleAsset.AddReference();
                    var femaleAssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(femaleAsset);
                    emote.AssetResults[BodyShape.FEMALE] = femaleAssetResult;
                }

                if (embeddedEmote.male == null || embeddedEmote.female == null)
                {
                    // If possible, only one allocation for both genders
                    AttachmentRegularAsset unisexAsset = CreateAttachmentAsset(embeddedEmote.prefab);
                    unisexAsset.AddReference();
                    var unisexAssetResult = new StreamableLoadingResult<AttachmentRegularAsset>(unisexAsset);

                    if (embeddedEmote.male == null)
                        emote.AssetResults[BodyShape.MALE] = unisexAssetResult;

                    if (embeddedEmote.female == null)
                        emote.AssetResults[BodyShape.FEMALE] = unisexAssetResult;
                }

                if (embeddedEmote.audioClip != null)
                {
                    emote.AudioAssetResults[BodyShape.MALE] = new StreamableLoadingResult<AudioClipData>(new AudioClipData(embeddedEmote.audioClip));
                    emote.AudioAssetResults[BodyShape.FEMALE] = new StreamableLoadingResult<AudioClipData>(new AudioClipData(embeddedEmote.audioClip));
                }

                emote.ManifestResult = null;

                generatedEmotes.Add(emote);
            }

            return generatedEmotes;
        }

        private static AttachmentRegularAsset CreateAttachmentAsset(GameObject glb)
        {
            var rendererInfos = new List<AttachmentRegularAsset.RendererInfo>();

            foreach (SkinnedMeshRenderer? renderer in glb.GetComponentsInChildren<SkinnedMeshRenderer>())
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(renderer, renderer.sharedMaterial));

            return new AttachmentRegularAsset(glb, rendererInfos, null);
        }
    }
}
