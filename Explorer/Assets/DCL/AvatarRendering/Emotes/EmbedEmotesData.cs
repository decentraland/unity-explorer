using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
    }

    [CreateAssetMenu(menuName = "DCL/Emotes/EmbedEmotes")]
    public class EmbedEmotesData : ScriptableObject
    {
        public EmbedEmote[] emotes;

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
    }
}
