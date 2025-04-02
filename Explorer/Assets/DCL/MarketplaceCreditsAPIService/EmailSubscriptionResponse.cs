using System;

namespace DCL.MarketplaceCreditsAPIService
{
    // TODO (Santi): Remove this! This check should be done directly by the progress endpoint
    [Serializable]
    public class EmailSubscriptionResponse
    {
        public string email;
        public string unconfirmedEmail;
    }
}
