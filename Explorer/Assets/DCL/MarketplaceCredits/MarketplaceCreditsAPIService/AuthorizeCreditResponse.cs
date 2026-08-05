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

        /// <summary>
        ///     Empty for a CollectionStore mint, which has no trade.
        ///     <para>
        ///         Neither this nor the item pair below is part of what the server SIGNS: the authorization is a
        ///         voucher for an AMOUNT (value + expiry + salt + signature) and the caps the CreditsManager
        ///         enforces on-chain against whatever MANA the external call actually moves. These fields are
        ///         recorded on the intent so the buyer's purchase history can name what was bought — and for a
        ///         mint they are the only identity it has, since there is no trade to resolve a name from.
        ///     </para>
        /// </summary>
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
