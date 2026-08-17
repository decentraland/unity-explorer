using System;

namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server GET /credits/orders/:orderId (Server/credits-server/src/controllers/handlers/get-order-status.ts).
    [Serializable]
    public struct CreditsOrderStatusResponse
    {
        public const string STATUS_PROCESSING = "processing";
        public const string STATUS_CREDITED = "credited";
        public const string STATUS_FAILED = "failed";

        // The checkout was retired without a payment — the buyer clicked back on Stripe's page, or the
        // session expired. Terminal: no payment can be taken against it any more, so a poll that keeps
        // waiting on it waits for something that can never arrive.
        public const string STATUS_ABANDONED = "abandoned";

        public string status;

        // Whole credits granted by the order.
        public int creditsGranted;

        // Whole spendable credits after the grant.
        public int newBalance;

        public string error;
    }
}
