using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct EmotesResolution
    {
        private readonly RepoolableList<IEmote> emotes;

        public EmotesResolution(RepoolableList<IEmote> emotes)
        {
            this.emotes = emotes;
        }

        public ConsumedList<IEmote> ConsumeEmotes() =>
            new (emotes);
    }
}
