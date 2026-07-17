namespace DCL.MarketplaceCredits.Purchase
{
    public enum CreditsPurchaseState
    {
        RESOLVING_LISTING,
        AUTHORIZING,
        SIGNING,
        SUBMITTING,
        WAITING_SETTLEMENT,
        SUCCESS,
        FAILED,
    }

    public enum CreditsPurchaseError
    {
        NONE,
        FEATURE_DISABLED,
        LISTING_NOT_AVAILABLE,
        OWN_LISTING,
        PRICE_CHANGED,
        INSUFFICIENT_CREDITS,
        AUTHORIZATION_FAILED,
        SIGNATURE_REJECTED,
        SIGNING_FAILED,
        RELAYER_UNAVAILABLE,
        TRANSACTION_REVERTED,
        SETTLEMENT_PENDING,
        CANCELLED,
        ENCODING_FAILED,
        UNKNOWN_ERROR,
    }

    public readonly struct CreditsPurchaseResult
    {
        public readonly CreditsPurchaseError Error;
        public readonly string? TxHash;
        public readonly string? Message;

        public bool Success => Error == CreditsPurchaseError.NONE;

        public CreditsPurchaseResult(CreditsPurchaseError error, string? txHash = null, string? message = null)
        {
            Error = error;
            TxHash = txHash;
            Message = message;
        }

        public static CreditsPurchaseResult Ok(string txHash) =>
            new (CreditsPurchaseError.NONE, txHash);
    }
}
