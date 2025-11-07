using UnityEngine;

namespace DCL.Backpack.Gifting.Views
{
    public readonly struct GiftTransferStatusParams
    {
        public readonly string recipientUserId;
        public readonly string recipientName;
        public readonly Sprite? userThumbnail;
        public readonly string giftUrn;
        public readonly string giftDisplayName;
        public readonly Sprite giftThumbnail;
        public readonly GiftItemStyleSnapshot style;

        public GiftTransferStatusParams(string recipientUserId,
            string recipientName,
            Sprite? userThumbnail,
            string giftUrn,
            string giftDisplayName,
            Sprite? giftThumbnail,
            GiftItemStyleSnapshot style)
        {
            this.recipientUserId = recipientUserId;
            this.recipientName = recipientName;
            this.userThumbnail = userThumbnail;
            this.giftUrn = giftUrn;
            this.giftDisplayName = giftDisplayName;
            this.giftThumbnail = giftThumbnail;
            this.style = style;
        }
    }
}