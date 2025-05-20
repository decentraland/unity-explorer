using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public struct ClaimCreditsResponse
    {
        public bool ok;
        public float credits_granted;
        public bool isBlockedForClaiming;
    }
}
