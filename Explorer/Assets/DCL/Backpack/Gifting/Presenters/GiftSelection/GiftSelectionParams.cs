using UnityEngine;

namespace DCL.Backpack.Gifting.Views
{
    public struct GiftSelectionParams
    {
        public readonly string userAddress;
        public readonly string userName;
        public readonly Color userNameColor;

        public GiftSelectionParams(string userAddress, string userName, Color userNameColor)
        {
            this.userAddress = userAddress;
            this.userName = userName;
            this.userNameColor = userNameColor;
        }
    }
}