using UnityEngine;

namespace DCL.Backpack.Gifting.Presenters
{
    public readonly struct GiftTransferSuccessParams
    {
        public readonly string RecipientName;
        public readonly Sprite? UserThumbnail;

        public GiftTransferSuccessParams(string recipientName, Sprite? userThumbnail)
        {
            RecipientName = recipientName;
            UserThumbnail = userThumbnail;
        }
    }
}