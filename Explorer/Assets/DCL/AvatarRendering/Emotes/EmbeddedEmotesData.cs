using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
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
        public GameObject prefab;
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

                AttachmentRegularAsset asset = CreateWearableAsset(embeddedEmote.prefab);
                asset.AddReference();
                var assetLoadResult = new StreamableLoadingResult<AttachmentRegularAsset>(asset);
                emote.AssetResults[BodyShape.MALE] = assetLoadResult;
                emote.AssetResults[BodyShape.FEMALE] = assetLoadResult;

                if (embeddedEmote.audioClip != null)
                {
                    emote.AudioAssetResults[BodyShape.MALE] = new StreamableLoadingResult<AudioClip>(embeddedEmote.audioClip);
                    emote.AudioAssetResults[BodyShape.FEMALE] = new StreamableLoadingResult<AudioClip>(embeddedEmote.audioClip);
                }

                emote.ManifestResult = null;

                generatedEmotes.Add(emote);
            }

            return generatedEmotes;
        }

        private static AttachmentRegularAsset CreateWearableAsset(GameObject glb)
        {
            var rendererInfos = new List<AttachmentRegularAsset.RendererInfo>();

            foreach (SkinnedMeshRenderer? renderer in glb.GetComponentsInChildren<SkinnedMeshRenderer>())
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(renderer, renderer.sharedMaterial));

            return new AttachmentRegularAsset(glb, rendererInfos, null);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (emotes.Length == 0)
            {
                var emotes = new List<EmbeddedEmote>();

                string[] thumbGUIDs = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbeddedEmotes/Thumbnails" });
                string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbeddedEmotes/Prefabs" });

                var sprites = new List<Sprite>();
                var prefabs = new List<GameObject>();

                foreach (string thumbGUID in thumbGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(thumbGUID);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    sprites.Add(sprite);
                }

                foreach (string prefabGUID in prefabGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    prefabs.Add(prefab);
                }

                foreach (GameObject prefab in prefabs)
                {
                    string nameWithoutSuffix = prefab.name.Replace("_Emote", "");

                    emotes.Add(new EmbeddedEmote
                    {
                        id = nameWithoutSuffix,
                        name = nameWithoutSuffix,
                        thumbnail = sprites.FirstOrDefault(t => t.name.Contains(prefab.name))!,
                        prefab = prefab,
                    });
                }

                this.emotes = emotes.ToArray();
            }
        }
#endif
    }
}
