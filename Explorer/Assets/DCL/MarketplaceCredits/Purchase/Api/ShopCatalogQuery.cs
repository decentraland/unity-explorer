using System.Collections.Generic;

namespace DCL.MarketplaceCredits.Purchase
{
    public enum ShopSort
    {
        Newest,
        Cheapest,
        MostExpensive,
        Name,
    }

    public enum ShopItemCategory
    {
        Any,
        Wearable,
        Emote,
    }

    public struct ShopCatalogQuery
    {
        public int First;
        public int Skip;
        public ShopItemCategory Category;
        public IReadOnlyList<string>? WearableCategories;
        public IReadOnlyList<string>? Rarities;
        public int? MinPriceCredits;
        public int? MaxPriceCredits;
        public string? Search;
        public ShopSort Sort;
        public bool SmartOnly;
        public bool? IsOnSale;
    }

    internal static class ShopCatalogQueryWire
    {
        public static string SortToWire(ShopSort sort) =>
            sort switch
            {
                ShopSort.Cheapest => "cheapest",
                ShopSort.MostExpensive => "most_expensive",
                ShopSort.Name => "name",
                _ => "newest",
            };

        public static string? CategoryToWire(ShopItemCategory category) =>
            category switch
            {
                ShopItemCategory.Wearable => "wearable",
                ShopItemCategory.Emote => "emote",
                _ => null,
            };
    }
}
