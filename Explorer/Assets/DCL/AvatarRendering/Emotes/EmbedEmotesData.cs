using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
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
    public struct EmbedEmote
    {
        public string id;
        public string name;
        public AnimationClip avatarClip;
        public AnimationClip propClip;
        public GameObject propModel;
        public AudioClip audioClip;
        public Sprite thumbnail;
        public bool loop;
    }

    [CreateAssetMenu(menuName = "DCL/Emotes/EmbedEmotes")]
    public class EmbedEmotesData : ScriptableObject
    {
        public EmbedEmote[] emotes;

        private List<IEmote>? generatedEmotes;

        public IEnumerable<IEmote> GenerateEmotes()
        {
            if (generatedEmotes != null) return generatedEmotes;

            generatedEmotes = new List<IEmote>();

            foreach (EmbedEmote embeddedEmote in emotes)
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
                model.metadata.data = new EmoteDTO.Metadata.Data();
                model.metadata.data.hides = Array.Empty<string>();
                model.metadata.data.replaces = Array.Empty<string>();
                model.metadata.data.removesDefaultHiding = Array.Empty<string>();
                model.metadata.data.tags = Array.Empty<string>();
                model.metadata.data.loop = embeddedEmote.loop;
                model.metadata.data.representations = Array.Empty<EmoteDTO.Metadata.Representation>();

                emote.Model = new StreamableLoadingResult<EmoteDTO>(model);
                emote.IsLoading = false;
                emote.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(embeddedEmote.thumbnail);

                // TODO: initialize valid assets
                emote.WearableAssetResults[BodyShape.MALE] = new StreamableLoadingResult<WearableAsset>(new Exception("Missing asset resolving"));
                emote.WearableAssetResults[BodyShape.FEMALE] = new StreamableLoadingResult<WearableAsset>(new Exception("Missing asset resolving"));

                // TODO: initialize manifest (?)
                emote.ManifestResult = null;

                generatedEmotes.Add(emote);
            }

            return generatedEmotes;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (emotes.Length == 0)
            {
                var emotes = new List<EmbedEmote>();

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
                    emotes.Add(new EmbedEmote
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
