using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct EmotesResolution
    {
        public int TotalAmount { get; }

        private readonly RepoolableList<IEmote> emotes;

        public EmotesResolution(RepoolableList<IEmote> emotes, int totalAmount)
        {
            this.emotes = emotes;
            TotalAmount = totalAmount;
        }

        public ConsumedList<IEmote> ConsumeEmotes() =>
            new (emotes);
    }
}
