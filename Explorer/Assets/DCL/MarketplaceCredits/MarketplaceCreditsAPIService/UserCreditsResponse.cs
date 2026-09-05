using System;

namespace DCL.MarketplaceCredits
{
    //Response of credits-server GET /users/:address/credits
    [Serializable]
    public struct UserCreditsResponse
    {
        public UserCreditItem[] credits;
        public double totalCredits;
        public CreditsTotals totals;
        public UsdCredits usd;
    }

    [Serializable]
    public struct UserCreditItem
    {
        public string id;
        public string userAddress;
        public string amount;
        public string availableAmount;
        public string status;
        public string contract;
        public string timestamp;
        public string signature;
        public int seasonId;
        public string goalId;
        public int weekId;
        public string claimedAt;
        public string expiresAt;
        public string creditSource;
    }

    [Serializable]
    public struct CreditsTotals
    {
        public double expiring;
        public double nonExpiring;
    }

    [Serializable]
    public struct UsdCredits
    {
        public long balanceCents;
        public int credits;
    }
}
