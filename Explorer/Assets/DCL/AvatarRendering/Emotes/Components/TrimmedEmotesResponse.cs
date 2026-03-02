using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct TrimmedEmotesResponse
    {
        public int TotalAmount { get; }

        public readonly IReadOnlyList<ITrimmedEmote> Emotes;

        public TrimmedEmotesResponse(IReadOnlyList<ITrimmedEmote> emotes, int totalAmount)
        {
            this.Emotes = emotes;
            TotalAmount = totalAmount;
        }
    }
}
