using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
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
        public AnimationClip avatarClip;
        public AnimationClip propClip;
        public GameObject propModel;
        public AudioClip audioClip;
        public Sprite thumbnail;
        public GameObject glbMale;
        public GameObject glbFemale;
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
                var emote = new Emote();
                var model = new EmoteDTO();
                model.id = embeddedEmote.id;

                var contents = new List<EmoteDTO.Content>();

                if (embeddedEmote.propClip != null)
                {
                    contents.Add(new EmoteDTO.Content
                    {
                        file = "propClip.anim",
                        hash = "propClip",
                    });
                }

                if (embeddedEmote.audioClip != null)
                {
                    contents.Add(new EmoteDTO.Content
                    {
                        file = "audioClip.mp3",
                        hash = "audioClip",
                    });
                }

                contents.Add(new EmoteDTO.Content
                {
                    file = "avatarClip.anim",
                    hash = "avatarClip",
                });

                contents.Add(new EmoteDTO.Content
                {
                    file = "thumbnail.png",
                    hash = "thumbnail",
                });

                model.content = contents.ToArray();
                model.pointers = new[] { embeddedEmote.id };
                model.type = "emote";
                model.version = "v3";
                model.metadata = new EmoteDTO.Metadata();
                model.metadata.id = embeddedEmote.id;
                model.metadata.name = embeddedEmote.name;

                model.metadata.i18n = new[]
                {
                    new EmoteDTO.Metadata.I18n
                    {
                        code = "en",
                        text = embeddedEmote.name,
                    },
                };

                model.metadata.thumbnail = "thumbnail";
                model.metadata.emoteDataADR74 = embeddedEmote.entity;

                emote.Model = new StreamableLoadingResult<EmoteDTO>(model);
                emote.IsLoading = false;
                emote.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(embeddedEmote.thumbnail);

                // TODO: solve rendererInfos (?)
                if (embeddedEmote.glbMale != null)
                    emote.WearableAssetResults[BodyShape.MALE] = new StreamableLoadingResult<WearableAsset>(
                        new WearableAsset(embeddedEmote.glbMale, new List<WearableAsset.RendererInfo>(), null));

                if (embeddedEmote.glbFemale != null)
                    emote.WearableAssetResults[BodyShape.FEMALE] = new StreamableLoadingResult<WearableAsset>(
                        new WearableAsset(embeddedEmote.glbFemale, new List<WearableAsset.RendererInfo>(), null));

                // TODO: initialize manifest (?)
                emote.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>(new Exception($"No existing asset bundle manifest for embedded emote {embeddedEmote.id}"));

                generatedEmotes.Add(emote);
            }

            return generatedEmotes;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (emotes.Length == 0)
            {
                var emotes = new List<EmbeddedEmote>();

                string[] clipGUIDs = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbedEmotes/Animations/" });
                string[] thumbGUIDs = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/DCL/AvatarRendering/AvatarShape/Assets/EmbedEmotes/Thumbnails" });

                var sprites = new List<Sprite>();
                var clips = new List<AnimationClip>();

                foreach (string thumbGUID in thumbGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(thumbGUID);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    sprites.Add(sprite);
                }

                foreach (string clipGUID in clipGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(clipGUID);
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    clips.Add(clip);
                }

                foreach (AnimationClip clip in clips)
                {
                    emotes.Add(new EmbeddedEmote
                    {
                        avatarClip = clip,
                        id = clip.name,
                        name = clip.name,
                        thumbnail = sprites.FirstOrDefault(t => t.name.Contains(clip.name))!,
                    });
                }

                this.emotes = emotes.ToArray();
            }
        }
#endif
    }
}
