using DCL.AvatarRendering.Emotes;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private EmbedEmotesData embedEmotesData;
        private Dictionary<string, EmoteData> emotes = new ();

        // todo: we want this here?
        private Dictionary<int, string> hotkeyEmotes = new ();

        public EmoteRepository(EmbedEmotesData embedEmotesData)
        {
            this.embedEmotesData = embedEmotesData;

            // todo: remove this
            emotes = embedEmotesData.emotes.ToDictionary(e => e.id, e => new EmoteData() { id = e.id, avatarClip = e.avatarClip, loop = e.avatarClip.isLooping});

            using var embedEmotes = emotes.Values.GetEnumerator();
            for (var i = 0; i < 8; i++)
            {
                var embedEmote = embedEmotes.Current;
                hotkeyEmotes.Add(i, embedEmote.id);
                embedEmotes.MoveNext();
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
