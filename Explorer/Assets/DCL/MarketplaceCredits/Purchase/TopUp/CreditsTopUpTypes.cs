namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public enum CreditsTopUpStage
    {
        IDLE,
        CREATING_CHECKOUT,
        WAITING_FOR_PAYMENT,
        PENDING_TIMEOUT,
        CREDITED,
        FAILED,
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
            new (CreditsTopUpStage.IDLE, default(CreditPack));

        public static CreditsTopUpStatus CreatingCheckout(CreditPack pack) =>
            new (CreditsTopUpStage.CREATING_CHECKOUT, pack);

        public static CreditsTopUpStatus WaitingForPayment(CreditPack pack, string orderId) =>
            new (CreditsTopUpStage.WAITING_FOR_PAYMENT, pack, orderId);

        public static CreditsTopUpStatus PendingTimeout(CreditPack pack, string orderId) =>
            new (CreditsTopUpStage.PENDING_TIMEOUT, pack, orderId);

        public static CreditsTopUpStatus Credited(CreditPack pack, string orderId, int creditsGranted, int newBalance) =>
            new (CreditsTopUpStage.CREDITED, pack, orderId, creditsGranted, newBalance);

        public static CreditsTopUpStatus CheckoutFailed(CreditPack pack, CreditsCheckoutError error, string? errorMessage) =>
            new (CreditsTopUpStage.FAILED, pack, errorMessage: errorMessage, checkoutError: error);

        public static CreditsTopUpStatus GrantFailed(CreditPack pack, string orderId, string? errorMessage) =>
            new (CreditsTopUpStage.FAILED, pack, orderId, errorMessage: errorMessage);
    }
}
