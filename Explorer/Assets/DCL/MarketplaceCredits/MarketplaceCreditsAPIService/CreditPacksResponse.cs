using System;

// ReSharper disable InconsistentNaming

namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server GET /credits/packs (Server/credits-server/src/controllers/handlers/get-credit-packs.ts).
    [Serializable]
    public struct CreditPacksResponse
    {
        public CreditPackData[] packs;
    }

    [Serializable]
    public struct CreditPackData
    {
        public string id;
        public float usd;
        public int credits;
        public bool recommended;
        public int order;
        public string imageUrl;
        public string imageUrlWebp; // browser-only; unused by the client
    }
}
