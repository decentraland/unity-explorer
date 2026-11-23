using DCL.MarketplaceCredits.Purchase.Cart.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>The cart popup being opened. The checkout it leads to is reported by <see cref="ShopCartCheckoutAnalytics" />.</summary>
    public class ShopCartAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ShopCartModalController controller;

        public ShopCartAnalytics(IAnalyticsController analytics, ShopCartModalController controller)
        {
            this.analytics = analytics;
            this.controller = controller;

            controller.CartOpened += OnCartOpened;
        }

        public void Dispose() =>
            controller.CartOpened -= OnCartOpened;

        private void OnCartOpened(string source, int cartSize, long cartCredits) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_CART_OPENED, new JObject
            {
                { "source", source },
                { "cart_size", cartSize },
                { "cart_value_credits", cartCredits },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
    }
}
