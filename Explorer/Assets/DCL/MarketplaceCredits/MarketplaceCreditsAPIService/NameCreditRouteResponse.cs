#nullable enable
// ReSharper disable InconsistentNaming

namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server GET /credits-name-route (Server/shop/app/src/lib/names.ts NameCreditRoute).
    public class NameCreditRouteResponse
    {
        public NameRouteExternalCall externalCall = null!;
        public string customExternalCallSignature = null!;
        public string? quoteId;
    }

    // Server schema: credits-server GET /credits-name-route (Server/shop/app/src/lib/names.ts NameCreditRoute.externalCall).
    public class NameRouteExternalCall
    {
        public string target = null!;
        public string selector = null!;
        public string data = null!;
        public long expiresAt;
        public string salt = null!;
    }
}
