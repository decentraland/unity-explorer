using System;

namespace DCL.Passport.Modules.Creations
{
    // Server schema: https://marketplace-api.decentraland.{ENV}/v3/catalog/items (GET, response.data[] — /v1/items shape plus priceCredits)
    [Serializable]
    public class MarketplaceCatalogResponse
    {
        public MarketplaceCatalogItem[]? data;
        public int total;
    }

    [Serializable]
    public class MarketplaceCatalogItem
    {
        public string? name;
        public string? thumbnail;
        public string? url;
        public string? urn;
        public string? rarity;
        public bool isOnSale;
        public int priceCredits;
        public MarketplaceCatalogItemData? data;
    }

    [Serializable]
    public class MarketplaceCatalogItemData
    {
        public MarketplaceCatalogItemCategory? wearable;
    }

    [Serializable]
    public class MarketplaceCatalogItemCategory
    {
        public string? category;
    }
}
