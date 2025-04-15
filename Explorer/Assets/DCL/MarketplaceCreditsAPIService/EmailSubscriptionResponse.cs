using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public struct EmailSubscriptionResponse
    {
        public string email;
        public string unconfirmedEmail;
    }
}
