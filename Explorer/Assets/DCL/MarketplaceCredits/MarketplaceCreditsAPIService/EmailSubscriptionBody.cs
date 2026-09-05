using System;

namespace DCL.MarketplaceCredits
{
    [Serializable]
    public struct EmailSubscriptionBody
    {
        public string email;
        public bool isCreditsWorkflow;
    }
}
