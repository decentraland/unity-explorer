using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: marketplace-server GET /v3/catalog/unified (Server/shop/app/src/lib/api.ts ShopListingRaw).
    // Rows are either Shop-native (priced directly in USD) or legacy MANA-priced, discriminated by source.
    [Serializable]
    public class ShopListingDto
    {
        public string tradeId = null!;
        public string? acquisition;
        public string listingType = null!;
        public string source = null!;
        public string contractAddress = null!;
        public string? itemId;
        public string? tokenId;
        public string name = null!;
        public string thumbnail = null!;
        public string rarity = null!;
        public string category = null!;
        public string? wearableCategory;
        public string creator = null!;
        public int priceCredits;
        public string? manaWei;
        public int available;
        public string network = null!;
        public int chainId;
        public int? compareAtCredits;
        public long? saleEndsAt;
    }

    // Server schema: marketplace-server GET /v3/catalog/unified response envelope.
    [Serializable]
    public class ShopListingsResponse
    {
        public ShopListingDto[]? data;
        public int total;
    }
}
