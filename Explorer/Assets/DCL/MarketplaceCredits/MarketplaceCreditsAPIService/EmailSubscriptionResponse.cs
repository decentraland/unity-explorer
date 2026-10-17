using System;

namespace DCL.MarketplaceCredits
{
    [Serializable]
    public struct EmailSubscriptionResponse
    {
        public string email;
        public string unconfirmedEmail;
    }
}
