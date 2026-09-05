namespace DCL.MarketplaceCredits
{
    public partial class CreditsUnlockedController
    {
        public readonly struct Params
        {
            // Amount of credits claimed to show in the reward panel
            public float ClaimedCredits { get; }

            public Params(float claimedCredits)
            {
                ClaimedCredits = claimedCredits;
            }
        }
    }
}
