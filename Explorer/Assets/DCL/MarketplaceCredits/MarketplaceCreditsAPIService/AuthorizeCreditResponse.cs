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
        // Trade id will be empty for a CollectionStore mint, for regular trades it will be the trade id.
        // when empty the item is identified by (contractAddress, itemId) pair instead as the server accepts either
        public string tradeId;
        public string contractAddress;
        public string itemId;
    }

    [Serializable]
    public struct ReleaseUsdIntentsBody
    {
        public string[] salts;
    }
}
