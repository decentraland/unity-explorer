using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct TrimmedEmotesResponse
    {
        public int TotalAmount { get; }

        private readonly IReadOnlyList<ITrimmedEmote> emotes;

        public TrimmedEmotesResponse(IReadOnlyList<ITrimmedEmote> emotes, int totalAmount)
        {
            this.emotes = emotes;
            TotalAmount = totalAmount;
        }
    }
}
