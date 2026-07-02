using System;

namespace DCL.Passport.Modules.Creations
{
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
