using DCL.MarketplaceCredits.Purchase;

namespace DCL.Shop
{
    public static class ShopQueryMapper
    {
        public const int PAGE_SIZE = 48;

        public static ShopCatalogQuery ToQuery(ShopCollectiblesFilters filters, int skip)
        {
            var query = new ShopCatalogQuery
            {
                First = PAGE_SIZE,
                Skip = skip,
                Category = ToItemCategory(filters.Category),
                WearableCategories = ToWearableCategories(filters.SubCategoryKey),
                Rarities = filters.Rarities.Count > 0 ? filters.Rarities : null,
                Search = filters.IsSearching ? filters.SearchText.Trim() : null,
                Sort = ToShopSort(filters.Sort),
                SmartOnly = filters.Smart,
            };

            if (filters.UsesUnifiedFeed)
            {
                query.MinPriceCredits = filters.MinPriceCredits;
                query.MaxPriceCredits = filters.MaxPriceCredits;
            }
            else if (filters.EffectiveStatus == ShopStatusFilter.NotForSale)
                query.IsOnSale = false;

            return query;
        }

        public static string[]? ToWearableCategories(string? subCategoryKey) =>
            subCategoryKey != null && ShopCategoryTree.SUB_CATEGORY_MAP.TryGetValue(subCategoryKey, out string[] categories) ? categories : null;

        public static ShopItemCategory ToItemCategory(string category) =>
            category switch
            {
                ShopCategoryTree.WEARABLE => ShopItemCategory.Wearable,
                ShopCategoryTree.EMOTE => ShopItemCategory.Emote,
                _ => ShopItemCategory.Any,
            };

        public static ShopSort ToShopSort(ShopSortOption sort) =>
            sort switch
            {
                ShopSortOption.Cheapest => ShopSort.Cheapest,
                ShopSortOption.MostExpensive => ShopSort.MostExpensive,
                ShopSortOption.Name => ShopSort.Name,
                _ => ShopSort.Newest,
            };

        public static string ToAnalyticsSort(ShopSortOption sort) =>
            sort switch
            {
                ShopSortOption.Cheapest => "price-asc",
                ShopSortOption.MostExpensive => "price-desc",
                ShopSortOption.Name => "name",
                _ => "newest",
            };

        public static string ToAnalyticsStatus(ShopStatusFilter status) =>
            status switch
            {
                ShopStatusFilter.All => "all",
                ShopStatusFilter.NotForSale => "not_for_sale",
                _ => "on_sale",
            };
    }
}
