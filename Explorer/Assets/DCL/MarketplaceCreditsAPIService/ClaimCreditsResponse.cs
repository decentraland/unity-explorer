using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public class ClaimCreditsResponse
    {
        public bool ok;
        public float claimedCredits;
    }
}
