using UnityEngine;

namespace DCL.Backpack.Gifting.Views
{
    public readonly struct GiftTransferParams
    {
        public readonly string recipientAddress;
        public readonly string recipientName;
        public readonly Sprite? userThumbnail;
        public readonly string giftUrn;
        public readonly string giftDisplayName;
        public readonly Sprite giftThumbnail;
        public readonly GiftItemStyleSnapshot style;
        public readonly string itemType;

        public GiftTransferParams(string recipientAddress,
            string recipientName,
            Sprite? userThumbnail,
            string giftUrn,
            string giftDisplayName,
            Sprite? giftThumbnail,
            GiftItemStyleSnapshot style,
            string itemType)
        {
            this.recipientAddress = recipientAddress;
            this.recipientName = recipientName;
            this.userThumbnail = userThumbnail;
            this.giftUrn = giftUrn;
            this.giftDisplayName = giftDisplayName;
            this.giftThumbnail = giftThumbnail;
            this.style = style;
            this.itemType = itemType;
        }
    }
}