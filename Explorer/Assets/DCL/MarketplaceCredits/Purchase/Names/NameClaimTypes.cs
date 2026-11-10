namespace DCL.MarketplaceCredits.Purchase.Names
{
    public enum NameAvailability
    {
        Available,
        Taken,

        /// <summary>The registrar could not be read. The credits-server repeats the check before it signs.</summary>
        Unknown,
    }

    /// <summary>
    ///     Where a claim has got to. The steps last very different amounts of time: Signing ends when the buyer
    ///     signs, Registering is the cross-chain bridge and runs for minutes.
    /// </summary>
    public enum NameClaimStage
    {
        Preparing,
        Signing,
        Confirming,
        Registering,
    }

    public enum NameClaimStatus
    {
        Registered,

        /// <summary>The payment may have been or was broadcast and its outcome is not known yet.</summary>
        Pending,
        Failed,
    }

    public enum NameClaimError
    {
        None,
        FeatureDisabled,
        NameUnavailable,
        PriceUnavailable,
        RouteUnavailable,
        RouteTooExpensive,
        InsufficientCredits,
        AuthorizationFailed,
        PriceChanged,
        SignatureRejected,
        SigningFailed,
        RelayerUnavailable,
        TransactionReverted,

        /// <summary>The payment settled but the bridge did not register the NAME. The credits were spent.</summary>
        NotRegistered,
        Cancelled,
        UnknownError,
    }

    public readonly struct NameClaimQuote
    {
        public readonly int UsdCents;
        public readonly int Credits;

        public NameClaimQuote(int usdCents, int credits)
        {
            UsdCents = usdCents;
            Credits = credits;
        }
    }

    public readonly struct NameQuoteResult
    {
        public readonly NameClaimError Error;
        public readonly NameClaimQuote Quote;

        public bool Success => Error == NameClaimError.None;

        public NameQuoteResult(NameClaimError error)
        {
            Error = error;
            Quote = default(NameClaimQuote);
        }

        public NameQuoteResult(NameClaimQuote quote)
        {
            Error = NameClaimError.None;
            Quote = quote;
        }
    }

    public readonly struct NameClaimResult
    {
        public readonly NameClaimStatus Status;
        public readonly NameClaimError Error;
        public readonly string? Message;

        public NameClaimResult(NameClaimStatus status, NameClaimError error = NameClaimError.None, string? message = null)
        {
            Status = status;
            Error = error;
            Message = message;
        }

        public static NameClaimResult Failed(NameClaimError error, string? message = null) =>
            new (NameClaimStatus.Failed, error, message);
    }
}
