using System;

namespace DCL.MarketplaceCredits
{
    public enum CreditsCheckoutError
    {
        Cancelled,
        FeatureDisabled,
        PaymentsUnavailable,
        UnknownPack,
        ProviderError,
        NetworkError,
    }

    public enum CreditsOrderPollError
    {
        Cancelled,
        NotFound,
        NetworkError,
    }

    // Server schema: credits-server error body, shared by all /credits/* endpoints
    // (Server/credits-server/src/controllers/handlers/*.ts).
    [Serializable]
    public class CreditsErrorResponse
    {
        public string error;
    }
}
