using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct GiftableAvatarAttachment
    {
        public readonly string Urn;
        public readonly string Name;
        public readonly string Category;
        public readonly string Rarity;
        public readonly int Amount;

        public readonly IThumbnailAttachment Attachment;

        public GiftableAvatarAttachment(ITrimmedWearable trimmed, int amount)
        {
            Attachment = trimmed;
            Urn = trimmed.GetUrn();
            Name = trimmed.GetName();
            Category = trimmed.GetCategory();
            Rarity = trimmed.GetRarity();
            Amount = amount;
        }

        public GiftableAvatarAttachment(IEmote trimmed, int amount)
        {
            Attachment = trimmed;
            Urn = trimmed.GetUrn();
            Name = trimmed.GetName();
            Category = trimmed.GetCategory();
            Rarity = trimmed.GetRarity();
            Amount = amount;
        }
    }
}