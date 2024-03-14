using DCL.AvatarRendering.Emotes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.CharacterMotion.Emotes
{
    public interface IEmoteRepository
    {
        public string GetHotkeyEmote(int index);

        public bool Exists(string emoteId);

        public EmoteData Get(string emoteId);
    }

    // PLACEHOLDER CLASS
    public class EmoteRepository : IEmoteRepository
    {
        private EmbeddedEmotesData embedEmotesData;
        private Dictionary<string, EmoteData> emotes = new ();

        // todo: we want this here?
        private Dictionary<int, string> hotkeyEmotes = new ();

        public EmoteRepository(EmbeddedEmotesData embedEmotesData)
        {
            this.embedEmotesData = embedEmotesData;

            // todo: remove this
            emotes = embedEmotesData.emotes.ToDictionary(e => e.id, e =>
            {
                AnimationClip clip = e.prefab.GetComponent<UnityEngine.Animation>().clip;

                return new EmoteData
                    { id = e.id, avatarClip = clip, loop = e.entity.loop };
            });

            using var embedEmotes = emotes.Values.GetEnumerator();

            for (var i = 0; i < 8; i++)
            {
                embedEmotes.MoveNext();
                var embedEmote = embedEmotes.Current;
                hotkeyEmotes.Add(i, embedEmote.id);
            }
        }

        public string GetHotkeyEmote(int index) =>
            hotkeyEmotes[index];

        public bool Exists(string emoteId) =>
            emotes.ContainsKey(emoteId);

        public EmoteData Get(string emoteId) =>
            emotes[emoteId];
    }
}
