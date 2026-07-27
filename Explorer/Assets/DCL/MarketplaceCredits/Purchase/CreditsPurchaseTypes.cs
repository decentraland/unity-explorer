namespace DCL.MarketplaceCredits.Purchase
{
    public enum CreditsPurchaseState
    {
        ResolvingListing,
        Authorizing,
        Signing,
        Submitting,
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
