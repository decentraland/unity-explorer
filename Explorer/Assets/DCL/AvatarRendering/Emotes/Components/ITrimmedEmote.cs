using DCL.AvatarRendering.Loading.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface ITrimmedEmote : ITrimmedAvatarAttachment<TrimmedEmoteDTO>
    {
        public int Amount { get; set; }
        new TrimmedEmoteDTO TrimmedDTO { get; }

        void ITrimmedAvatarAttachment.SetAmount(int amount)
        {
            Amount = amount;
        }

    }
}
