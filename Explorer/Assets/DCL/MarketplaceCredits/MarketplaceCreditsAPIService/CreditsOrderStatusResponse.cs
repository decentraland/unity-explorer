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

        public string status;

        // Whole credits granted by the order.
        public int creditsGranted;

        // Whole spendable credits after the grant.
        public int newBalance;

        public string error;
    }
}
