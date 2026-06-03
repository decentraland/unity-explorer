using System;
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
        public readonly string tokenId;
        public readonly string instanceUrn;
        public readonly string userNameColorHex;

        // The gifted copy's last transfer-in timestamp at gift time; used as the pending-prune baseline.
        public readonly DateTime transferredAt;

        public GiftTransferParams(string recipientAddress,
            string recipientName,
            Sprite? userThumbnail,
            string giftUrn,
            string giftDisplayName,
            Sprite? giftThumbnail,
            GiftItemStyleSnapshot style,
            string itemType,
            string tokenId,
            string instanceUrn,
            string userNameColorHex,
            DateTime transferredAt)
        {
            this.recipientAddress = recipientAddress;
            this.recipientName = recipientName;
            this.userThumbnail = userThumbnail;
            this.giftUrn = giftUrn;
            this.giftDisplayName = giftDisplayName;
            this.giftThumbnail = giftThumbnail;
            this.style = style;
            this.itemType = itemType;
            this.tokenId = tokenId;
            this.instanceUrn = instanceUrn;
            this.userNameColorHex = userNameColorHex;
            this.transferredAt = transferredAt;
        }
    }
}