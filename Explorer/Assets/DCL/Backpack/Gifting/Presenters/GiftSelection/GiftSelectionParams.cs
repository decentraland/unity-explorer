namespace DCL.Backpack.Gifting.Views
{
    public struct GiftSelectionParams
    {
        public readonly string userAddress;
        public readonly string userName;

        public GiftSelectionParams(string userAddress, string userName)
        {
            this.userAddress  = userAddress;
            this.userName = userName;
        }
    }
}