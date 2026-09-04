using DCL.ExplorePanel;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Shop;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>The shop section funnel: page views, searches, filters and cart additions, named like the web shop's events.</summary>
    public class ShopAnalytics : IDisposable
    {
        private const double USD_PER_CREDIT = 0.1;

        private readonly IAnalyticsController analytics;
        private readonly ExplorePanelController explorePanelController;
        private readonly ShopController shopController;

        public ShopAnalytics(IAnalyticsController analytics, ExplorePanelController explorePanelController)
        {
            this.analytics = analytics;
            this.explorePanelController = explorePanelController;
            shopController = explorePanelController.ShopController;

            explorePanelController.ShopOpenedFromStartMenu += OnShopOpenedFromStartMenu;
            shopController.PageViewed += OnPageViewed;
            shopController.CollectiblesController.Searched += OnSearched;
            shopController.CollectiblesController.FilterApplied += OnFilterApplied;
            shopController.OverviewController.OutfitAddedToCart += OnOutfitAddedToCart;
            shopController.Cart.ItemAdded += OnItemAddedToCart;
            shopController.Cart.ItemRemoved += OnItemRemovedFromCart;
        }

        public void Dispose()
        {
            explorePanelController.ShopOpenedFromStartMenu -= OnShopOpenedFromStartMenu;
            shopController.PageViewed -= OnPageViewed;
            shopController.CollectiblesController.Searched -= OnSearched;
            shopController.CollectiblesController.FilterApplied -= OnFilterApplied;
            shopController.OverviewController.OutfitAddedToCart -= OnOutfitAddedToCart;
            shopController.Cart.ItemAdded -= OnItemAddedToCart;
            shopController.Cart.ItemRemoved -= OnItemRemovedFromCart;
        }

        private void OnShopOpenedFromStartMenu() =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_SECTION_OPENED, WithPlatform(new JObject { { "source", "start_menu" } }));

        private void OnPageViewed(ShopPage page) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_VIEWED_PAGE, WithPlatform(new JObject
            {
                { "page", page == ShopPage.Overview ? AnalyticsEvents.Shop.PAGE_OVERVIEW : AnalyticsEvents.Shop.PAGE_ASSETS },
            }));

        private void OnSearched(string query, int resultCount) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_SEARCHED, WithPlatform(new JObject
            {
                { "query", query },
                { "result_count", resultCount },
            }));

        private void OnFilterApplied(ShopCollectiblesFilters filters, int resultCount)
        {
            var rarities = new JArray();

            foreach (string rarity in filters.Rarities)
                rarities.Add(rarity);

            analytics.Track(AnalyticsEvents.Shop.SHOP_APPLIED_FILTER, WithPlatform(new JObject
            {
                {
                    "filters", new JObject
                    {
                        { "category", filters.Category },
                        { "sub_category", filters.SubCategoryKey },
                        { "rarities", rarities },
                        { "min_price_credits", filters.MinPriceCredits },
                        { "max_price_credits", filters.MaxPriceCredits },
                        { "status", ShopQueryMapper.ToAnalyticsStatus(filters.EffectiveStatus) },
                        { "smart", filters.Smart },
                        { "sort", ShopQueryMapper.ToAnalyticsSort(filters.Sort) },
                    }
                },
                { "result_count", resultCount },
            }));
        }

        private void OnItemAddedToCart(ShopCartLine line, ShopCartSource source)
        {
            ShopListingDto listing = line.Listing;
            ShopCart cart = shopController.Cart;

            analytics.Track(AnalyticsEvents.Shop.SHOP_ADDED_TO_CART, WithPlatform(new JObject
            {
                { "item_id", listing.itemId },
                { "contract_address", listing.contractAddress },
                { "price_credits", listing.priceCredits },
                { "price_usd", listing.priceCredits * USD_PER_CREDIT },
                { "category", listing.category },
                { "is_smart", listing.isSmart == true },
                { "is_primary", listing.IsPrimary() },
                { "source", ShopCartSources.ToWire(source) },
                { "cart_size", cart.Count },
                { "cart_value_usd", cart.TotalCredits * USD_PER_CREDIT },
            }));
        }

        private void OnItemRemovedFromCart(ShopCartLine line) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_REMOVED_FROM_CART, WithPlatform(new JObject
            {
                { "item_id", line.Listing.itemId },
                { "cart_size", shopController.Cart.Count },
            }));

        private void OnOutfitAddedToCart(ShopOutfitAddResult result) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_OUTFIT_ADDED_TO_CART, WithPlatform(new JObject
            {
                { "outfit_id", result.OutfitId },
                { "items_added", result.Added },
                { "items_skipped_unavailable", result.SkippedUnavailable },
                { "items_skipped_in_cart", result.SkippedInCart },
                { "items_skipped_own", result.SkippedOwn },
                { "total_credits", result.TotalCredits },
            }));

        private static JObject WithPlatform(JObject props)
        {
            props.Add(AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE);
            return props;
        }
    }
}
