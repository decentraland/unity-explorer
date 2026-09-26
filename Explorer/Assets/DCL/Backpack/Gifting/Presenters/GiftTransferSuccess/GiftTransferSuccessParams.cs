using UnityEngine;

namespace DCL.Backpack.Gifting.Presenters
{
    public readonly struct GiftTransferSuccessParams
    {
        public readonly string RecipientName;
        public readonly string UserNameColorHex;
        public readonly Sprite? UserThumbnail;

        public GiftTransferSuccessParams(string recipientName,
            Sprite? userThumbnail,
            string userNameColorHex)
        {
            RecipientName = recipientName;
            UserThumbnail = userThumbnail;
            UserNameColorHex = userNameColorHex;
        }
    }
}