namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public enum CreditsTopUpStage
    {
        Idle,
        CreatingCheckout,
        WaitingForPayment,
        PendingTimeout,
        Credited,
        Abandoned,
        Failed,
    }

    public readonly struct CreditsTopUpStatus
    {
        public readonly CreditsTopUpStage Stage;
        public readonly CreditPack Pack;
        public readonly string? OrderId;
        public readonly int CreditsGranted;
        public readonly int NewBalance;
        public readonly string? ErrorMessage;
        public readonly CreditsCheckoutError? CheckoutError;

        private CreditsTopUpStatus(CreditsTopUpStage stage, CreditPack pack, string? orderId = null,
            int creditsGranted = 0, int newBalance = 0, string? errorMessage = null, CreditsCheckoutError? checkoutError = null)
        {
            Stage = stage;
            Pack = pack;
            OrderId = orderId;
            CreditsGranted = creditsGranted;
            NewBalance = newBalance;
            ErrorMessage = errorMessage;
            CheckoutError = checkoutError;
        }

        public static CreditsTopUpStatus Idle() =>
            new (CreditsTopUpStage.Idle, default(CreditPack));

        public static CreditsTopUpStatus CreatingCheckout(CreditPack pack) =>
            new (CreditsTopUpStage.CreatingCheckout, pack);

        public static CreditsTopUpStatus WaitingForPayment(CreditPack pack, string orderId) =>
            new (CreditsTopUpStage.WaitingForPayment, pack, orderId);

        public static CreditsTopUpStatus PendingTimeout(CreditPack pack, string orderId) =>
            new (CreditsTopUpStage.PendingTimeout, pack, orderId);

        public static CreditsTopUpStatus Credited(CreditPack pack, string orderId, int creditsGranted, int newBalance) =>
            new (CreditsTopUpStage.Credited, pack, orderId, creditsGranted, newBalance);

        public static CreditsTopUpStatus CheckoutFailed(CreditPack pack, CreditsCheckoutError error, string? errorMessage) =>
            new (CreditsTopUpStage.Failed, pack, errorMessage: errorMessage, checkoutError: error);

        public static CreditsTopUpStatus GrantFailed(CreditPack pack, string orderId, string? errorMessage) =>
            new (CreditsTopUpStage.Failed, pack, orderId, errorMessage: errorMessage);

        /// <summary>
        /// The checkout was retired without a payment. Kept apart from Failed on purpose: nothing went
        /// wrong and nobody was charged, so the UI should say "cancelled" rather than raise an error.
        /// </summary>
        public static CreditsTopUpStatus Abandoned(CreditPack pack, string orderId) =>
            new (CreditsTopUpStage.Abandoned, pack, orderId);
    }
}
