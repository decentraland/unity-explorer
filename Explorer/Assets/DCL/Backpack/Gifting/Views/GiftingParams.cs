namespace DCL.Backpack.Gifting.Views
{
    public struct GiftingParams
    {
        public readonly string userId;
        public readonly string userName;

        public GiftingParams(string userId, string userName)
        {
            this.userId  = userId;
            this.userName = userName;
        }
    }
}