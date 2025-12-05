using DCL.AvatarRendering.Loading.Components;

namespace DCL.Backpack.Gifting.Models
{
    public readonly struct GiftableAvatarAttachment
    {
        public readonly IAvatarAttachment Attachment;

        public string Urn => Attachment.GetUrn();
        public string Name => Attachment.GetName();
        public string Category => Attachment.GetCategory();
        public string Rarity => Attachment.GetRarity();
        public int Amount => Attachment.Amount;

        public GiftableAvatarAttachment(IAvatarAttachment attachment)
        {
            Attachment = attachment;
        }
    }
}