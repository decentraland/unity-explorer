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

    public enum NamesSort
    {
        Newest,
        Cheapest,

        /// <summary>
        ///     The order of a search: how close each name is to the searched text.
        /// </summary>
        BestMatch,
    }

    public struct NamesOnSaleQuery
    {
        public int First;
        public int Skip;
        public string? Search;
        public NamesSort Sort;
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

        public static string? NamesSortToWire(NamesSort sort) =>
            sort switch
            {
                NamesSort.Newest => "newest",
                NamesSort.Cheapest => "cheapest",
                _ => null,
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
