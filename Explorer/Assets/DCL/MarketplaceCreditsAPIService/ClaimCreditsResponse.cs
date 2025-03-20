using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public class ClaimCreditsResponse
    {
        public bool success;
        public float claimedCredits;
    }
}
