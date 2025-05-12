using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public struct EmailSubscriptionBody
    {
        public string email;
        public bool isCreditsWorkflow;
    }
}
