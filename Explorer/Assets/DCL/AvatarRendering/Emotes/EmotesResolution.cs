using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public struct EmotesResolution
    {
        public static readonly EmotesResolution EMPTY = new (new List<IEmote>());

        /// <summary>
        ///     Poolable collection of result wearables
        /// </summary>
        public readonly List<IEmote> Emotes;

        public EmotesResolution(List<IEmote> emotes)
        {
            Emotes = emotes;
        }
    }
}
