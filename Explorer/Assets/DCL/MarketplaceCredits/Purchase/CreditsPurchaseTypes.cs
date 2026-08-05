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

    public readonly struct CreditsPurchaseQuote
    {
        public readonly TradeDto Trade;
        public readonly int UsdCents;
        public readonly int Credits;
        public readonly BigInteger RequiredManaWei;
        public readonly bool IsLiveRatePrice;

        public CreditsPurchaseQuote(TradeDto trade, int usdCents, int credits, BigInteger requiredManaWei, bool isLiveRatePrice)
        {
            Trade = trade;
            UsdCents = usdCents;
            Credits = credits;
            RequiredManaWei = requiredManaWei;
            IsLiveRatePrice = isLiveRatePrice;
        }
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
