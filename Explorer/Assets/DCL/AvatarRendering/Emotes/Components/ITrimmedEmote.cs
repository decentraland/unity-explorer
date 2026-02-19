using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface ITrimmedEmote : ITrimmedAvatarAttachment
    {
        public int Amount { get; set; }
        TrimmedEmoteDTO TrimmedDTO { get; }

        void ITrimmedAvatarAttachment.SetAmount(int amount)
        {
            Amount = amount;
        }

    }
}
