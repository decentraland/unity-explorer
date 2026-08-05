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
    /// <summary>
    ///     A TRADE-backed purchase: identified by its trade.
    ///     <para>
    ///         Deliberately a separate shape from the mint body below rather than one struct with every field.
    ///         `JsonUtility` cannot omit a field — it serialises every public member — and the server validates
    ///         `contractAddress`/`itemId` as a pair the moment EITHER key is present. An empty string counts as
    ///         present and fails the address check, which is exactly how a trade purchase came back 400 while the
    ///         mint went through. Two shapes make "absent" expressible.
    ///     </para>
    ///     <para>
    ///         Note what is NOT here: nothing identifying is part of what the server SIGNS. The authorization is
    ///         a voucher for an AMOUNT (value + expiry + salt + signature) plus the caps the CreditsManager
    ///         enforces on-chain against whatever MANA the external call actually moves. The identifiers are
    ///         recorded on the intent so the buyer's purchase history can name what was bought.
    ///     </para>
    /// </summary>
    public struct AuthorizeUsdCreditBody
    {
        public int usdPriceCents;
        public string tradeId;
    }

    /// <summary>
    ///     A COLLECTIONSTORE MINT: it has no trade, so it is identified by what is being bought. The server takes
    ///     the pair only as a pair — half of it resolves to nothing.
    /// </summary>
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
