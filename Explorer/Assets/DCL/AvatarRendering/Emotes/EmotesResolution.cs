using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct EmotesResolution
    {
        /// <summary>
        ///     Poolable collection of result wearables
        /// </summary>
        public IReadOnlyList<IEmote> Emotes { get; }
        public int TotalAmount { get; }

        public EmotesResolution(IReadOnlyList<IEmote> emotes, int totalAmount)
        {
            Emotes = emotes;
            TotalAmount = totalAmount;
        }
    }
}
