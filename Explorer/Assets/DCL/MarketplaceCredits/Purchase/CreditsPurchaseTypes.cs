using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    public enum CreditsPurchaseState
    {
        ResolvingListing,
        Authorizing,
        Signing,
        WaitingSettlement,
        Success,
        Failed,
    }

    public enum CreditsPurchaseError
    {
        None,
        FeatureDisabled,
        ListingNotAvailable,
        OwnListing,
        PriceChanged,
        PriceUnavailable,
        InsufficientCredits,
        AuthorizationFailed,
        SignatureRejected,
        SigningFailed,
        RelayerUnavailable,
        TransactionReverted,
        SettlementPending,
        Cancelled,
        EncodingFailed,
        UnknownError,
    }

    /// <summary>
    ///     How a listing is bought. NOT a display detail — it picks the external call the CreditsManager makes,
    ///     and the two are mutually exclusive: an offchain trade has a signed order to accept, a CollectionStore
    ///     mint has nothing signed at all and is minted straight from the store contract.
    /// </summary>
    public enum CreditsListingKind
    {
        Trade,
        StoreMint,
    }

    /// <summary>
    ///     A CollectionStore mint: the primary-sale path for items that were never listed as a trade, so there is
    ///     no tradeId, no orderId, and nothing to fetch from /v1/trades.
    /// </summary>
    public readonly struct StoreMintTarget
    {
        public readonly string CollectionAddress;
        public readonly string ItemId;

        /// <summary>
        ///     MANA wei, as the contract will verify it. Re-read as late as possible: CollectionStore.buy takes
        ///     the price as an argument and re-validates it against the item's live price, so a stale value is a
        ///     revert rather than a wrong number.
        /// </summary>
        public readonly string PriceWei;

        public StoreMintTarget(string collectionAddress, string itemId, string priceWei)
        {
            CollectionAddress = collectionAddress;
            ItemId = itemId;
            PriceWei = priceWei;
        }
    }

    public readonly struct CreditsPurchaseQuote
    {
        public readonly CreditsListingKind Kind;

        /// <summary>Null for a mint — see <see cref="CreditsListingKind" />.</summary>
        public readonly TradeDto? Trade;

        /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="CreditsListingKind.StoreMint" />.</summary>
        public readonly StoreMintTarget Mint;

        public readonly int UsdCents;
        public readonly int Credits;
        public readonly BigInteger RequiredManaWei;
        public readonly bool IsLiveRatePrice;

        /// <summary>
        ///     What the credit intent records, so the purchase history can name the item. A mint has no trade, so
        ///     it is identified by the pair instead — the server takes either, and signs neither.
        /// </summary>
        // Null-tolerant: a default(CreditsPurchaseQuote) reads as a Trade with no trade, and this must not throw
        // on the way to reporting the failure.
        public string TradeId => Kind == CreditsListingKind.Trade ? Trade?.id ?? string.Empty : string.Empty;

        public string ContractAddress => Kind == CreditsListingKind.StoreMint ? Mint.CollectionAddress : string.Empty;

        public string ItemId => Kind == CreditsListingKind.StoreMint ? Mint.ItemId : string.Empty;

        private CreditsPurchaseQuote(
            CreditsListingKind kind,
            TradeDto? trade,
            StoreMintTarget mint,
            int usdCents,
            int credits,
            BigInteger requiredManaWei,
            bool isLiveRatePrice)
        {
            Kind = kind;
            Trade = trade;
            Mint = mint;
            UsdCents = usdCents;
            Credits = credits;
            RequiredManaWei = requiredManaWei;
            IsLiveRatePrice = isLiveRatePrice;
        }

        public static CreditsPurchaseQuote ForTrade(TradeDto trade, int usdCents, int credits, BigInteger requiredManaWei, bool isLiveRatePrice) =>
            new (CreditsListingKind.Trade, trade, default(StoreMintTarget), usdCents, credits, requiredManaWei, isLiveRatePrice);

        /// <summary>
        ///     A mint is always MANA-denominated, so its MANA is known up front (isLiveRatePrice: true) and its
        ///     credit price exists only through the oracle — exactly like a legacy MANA trade.
        /// </summary>
        public static CreditsPurchaseQuote ForMint(StoreMintTarget mint, int usdCents, int credits, BigInteger requiredManaWei) =>
            new (CreditsListingKind.StoreMint, null, mint, usdCents, credits, requiredManaWei, true);
    }

    public readonly struct CreditsQuoteResult
    {
        public readonly CreditsPurchaseError Error;
        public readonly CreditsPurchaseQuote Quote;
        public readonly string? Message;

        public bool Success => Error == CreditsPurchaseError.None;

        public CreditsQuoteResult(CreditsPurchaseError error, string? message = null)
        {
            Error = error;
            Quote = default(CreditsPurchaseQuote);
            Message = message;
        }

        private CreditsQuoteResult(in CreditsPurchaseQuote quote)
        {
            Error = CreditsPurchaseError.None;
            Quote = quote;
            Message = null;
        }

        public static CreditsQuoteResult Ok(in CreditsPurchaseQuote quote) =>
            new (quote);
    }

    public readonly struct CreditsPurchaseResult
    {
        public readonly CreditsPurchaseError Error;
        public readonly string? TxHash;
        public readonly string? Message;

        public bool Success => Error == CreditsPurchaseError.None;

        public CreditsPurchaseResult(CreditsPurchaseError error, string? txHash = null, string? message = null)
        {
            Error = error;
            TxHash = txHash;
            Message = message;
        }

        public static CreditsPurchaseResult Ok(string txHash) =>
            new (CreditsPurchaseError.None, txHash);
    }
}
