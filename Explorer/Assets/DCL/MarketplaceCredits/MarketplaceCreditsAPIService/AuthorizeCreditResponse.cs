using System;

namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server POST /credits/authorize (Server/shop/app/src/lib/credits.ts AuthorizeResult).
    [Serializable]
    public struct AuthorizeCreditResponse
    {
        public AuthorizedCredit credit;
        public string maxCreditedValue;
        public long usdCents;
        public string oracleRate;
    }

    // Server schema: credits-server POST /credits/authorize (Server/shop/app/src/lib/credits.ts AuthorizedCredit).
    // A single-use ephemeral credit signed by the credits-server; its id doubles as the intent salt for
    // POST /credits/authorize/cancel.
    [Serializable]
    public struct AuthorizedCredit
    {
        public string id;
        public string amount;
        public string availableAmount;
        public long expiresAt;
        public string signature;
        public string contract;
    }

    [Serializable]
    public struct AuthorizeUsdCreditBody
    {
        public int usdPriceCents;
        public string tradeId;
    }

    [Serializable]
    public struct AuthorizeUsdMintCreditBody
    {
        public int usdPriceCents;
        public string contractAddress;
        public string itemId;
    }

    [Serializable]
    public struct ReleaseUsdIntentsBody
    {
        public string[] salts;
    }
}
