using System;

namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server POST /credits/checkout (Server/credits-server/src/controllers/handlers/create-checkout-session.ts).
    [Serializable]
    public struct CheckoutResponse
    {
        public string orderId;
        public string url;
    }

    [Serializable]
    public struct CheckoutRequestBody
    {
        public string packId;
        public string source;
    }
}
