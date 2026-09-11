using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.MarketplaceCredits.Purchase.Cart.UI;
using DCL.MarketplaceCredits.Purchase.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>The cart checkout funnel, shaped like the web shop's purchase events so both clients reconcile in one warehouse.</summary>
    public class ShopCartAnalytics : IDisposable
    {
        private const double USD_PER_CREDIT = 0.1;
        private const string CHECKOUT_SOURCE_CART = "cart";
        private const string PURCHASE_TYPE_PRIMARY = "item";
        private const string PURCHASE_TYPE_RESALE = "nft_resale";
        private const string PAYMENT_TYPE_CREDITS = "credits";
        private const string BUY_CREDITS_FROM = "cart_checkout";

        private readonly IAnalyticsController analytics;
        private readonly ShopCartModalController controller;

        public ShopCartAnalytics(IAnalyticsController analytics, ShopCartModalController controller)
        {
            this.analytics = analytics;
            this.controller = controller;

            controller.CartOpened += OnCartOpened;
            controller.CheckoutStarted += OnCheckoutStarted;
            controller.CheckoutCompleted += OnCheckoutCompleted;
            controller.CheckoutFailed += OnCheckoutFailed;
            controller.CheckoutCancelled += OnCheckoutCancelled;
            controller.BuyCreditsPrompted += OnBuyCreditsPrompted;
        }

        public void Dispose()
        {
            controller.CartOpened -= OnCartOpened;
            controller.CheckoutStarted -= OnCheckoutStarted;
            controller.CheckoutCompleted -= OnCheckoutCompleted;
            controller.CheckoutFailed -= OnCheckoutFailed;
            controller.CheckoutCancelled -= OnCheckoutCancelled;
            controller.BuyCreditsPrompted -= OnBuyCreditsPrompted;
        }

        private void OnCartOpened(string source, int cartSize, int cartCredits) =>
            analytics.Track(AnalyticsEvents.Shop.SHOP_CART_OPENED, WithPlatform(new JObject
            {
                { "source", source },
                { "cart_size", cartSize },
                { "cart_value_credits", cartCredits },
            }));

        private void OnCheckoutStarted(int cartSize, int cartCredits, bool hasSufficientCredits) =>
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_STARTED_CHECKOUT, WithPlatform(new JObject
            {
                { "checkout_source", CHECKOUT_SOURCE_CART },
                { "cart_size", cartSize },
                { "cart_value_credits", cartCredits },
                { "cart_value_usd", cartCredits * USD_PER_CREDIT },
                { "has_sufficient_credits", hasSufficientCredits },
            }));

        // Web purchaseItemsProps: per-unit rows, the total, and what came from an outfit.
        private void OnCheckoutCompleted(CartCheckoutResult result)
        {
            var items = new JArray();
            var outfitIds = new SortedSet<string>(StringComparer.Ordinal);
            var valueCredits = 0;
            var unitsFromOutfit = 0;
            var anyPrimary = false;

            foreach (ReviewedCartLine unit in result.BoughtUnits)
            {
                ShopListingDto listing = unit.Line.Listing;
                valueCredits += unit.UnitCredits;
                anyPrimary |= listing.IsPrimary();

                if (unit.Line.OutfitId != null)
                {
                    outfitIds.Add(unit.Line.OutfitId);
                    unitsFromOutfit++;
                }

                items.Add(new JObject
                {
                    { "item_id", listing.itemId },
                    { "contract_address", listing.contractAddress },
                    { "token_id", listing.tokenId },
                    { "price_usd", unit.UnitCredits * USD_PER_CREDIT },
                    { "category", listing.category },
                    { "is_smart", listing.isSmart == true },
                    { "source", ShopCartSources.ToWire(unit.Line.Source) },
                    { "outfit_id", unit.Line.OutfitId },
                });
            }

            JArray? outfitIdsArray = null;

            if (outfitIds.Count > 0)
            {
                outfitIdsArray = new JArray();

                foreach (string outfitId in outfitIds)
                    outfitIdsArray.Add(outfitId);
            }

            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_COMPLETED_PURCHASE, WithPlatform(new JObject
            {
                { "items", items },
                { "value_credits", valueCredits },
                { "value_usd", valueCredits * USD_PER_CREDIT },
                { "purchase_type", anyPrimary ? PURCHASE_TYPE_PRIMARY : PURCHASE_TYPE_RESALE },
                { "is_primary", anyPrimary },
                { "outfit_ids", outfitIdsArray },
                { "units_from_outfit", unitsFromOutfit },
                { "payment_type", PAYMENT_TYPE_CREDITS },
                { "no_crypto_step", true },
                { "transaction_hash", result.SettledTxHashes.Count > 0 ? result.SettledTxHashes[0] : null },
                { "partial", result.Outcome == CartCheckoutOutcome.PartiallyCompleted },
            }));
        }

        private void OnCheckoutFailed(CartCheckoutResult result, string step, string errorCode) =>
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_PURCHASE_FAILED, WithPlatform(FailureProps(result, step, errorCode)));

        private void OnCheckoutCancelled(CartCheckoutResult result, string step) =>
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_PURCHASE_CANCELLED, WithPlatform(FailureProps(result, step, CreditPurchaseModalController.MapAnalyticsErrorCode(result.FirstError))));

        private void OnBuyCreditsPrompted(int creditsNeeded, int creditsBalance, int shortfall) =>
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_PROMPTED, WithPlatform(new JObject
            {
                { "from", BUY_CREDITS_FROM },
                { "credits_needed", creditsNeeded },
                { "credits_balance", creditsBalance },
                { "shortfall", shortfall },
            }));

        private static JObject FailureProps(CartCheckoutResult result, string step, string errorCode)
        {
            var unboughtCredits = 0;

            foreach (ReviewedCartLine unit in result.UnboughtUnits)
                unboughtCredits += unit.UnitCredits;

            return new JObject
            {
                { "step", step },
                { "error_code", errorCode },
                { "error_detail", CreditPurchaseModalController.MapAnalyticsErrorDetail(result.FirstError) },
                { "value_usd", unboughtCredits * USD_PER_CREDIT },
                { "cart_size", result.UnboughtUnits.Count + result.BoughtUnits.Count },
                { "partial", result.Outcome == CartCheckoutOutcome.PartiallyCompleted },
                { "held_credits", result.HasPendingSettlement },
            };
        }

        private static JObject WithPlatform(JObject props)
        {
            props.Add(AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE);
            return props;
        }
    }
}
