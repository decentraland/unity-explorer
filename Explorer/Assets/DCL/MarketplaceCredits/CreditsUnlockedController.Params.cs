namespace DCL.MarketplaceCredits
{
    public partial class CreditsUnlockedController
    {
        public struct Params
        {
            public float ClaimedCredits { get; }

            public Params(float claimedCredits)
            {
                ClaimedCredits = claimedCredits;
            }
        }
    }
}
