using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class CreditPurchaseAnalytics : IDisposable
    {
        private const string PURCHASE_TYPE_PRIMARY = "item";
        private const string PURCHASE_TYPE_RESALE = "nft_resale";

        private readonly IAnalyticsController analytics;
        private readonly CreditPurchaseModalController creditPurchaseModalController;

        public CreditPurchaseAnalytics(
            IAnalyticsController analytics,
            CreditPurchaseModalController creditPurchaseModalController)
        {
            this.analytics = analytics;
            this.creditPurchaseModalController = creditPurchaseModalController;

            this.creditPurchaseModalController.ModalOpened += OnModalOpened;
            this.creditPurchaseModalController.BuyCreditsPrompted += OnBuyCreditsPrompted;
            this.creditPurchaseModalController.PurchaseStarted += OnPurchaseStarted;
            this.creditPurchaseModalController.PurchaseCompleted += OnPurchaseCompleted;
            this.creditPurchaseModalController.PurchaseFailed += OnPurchaseFailed;
            this.creditPurchaseModalController.PurchaseCancelled += OnPurchaseCancelled;
            this.creditPurchaseModalController.NavigationClicked += OnNavigationClicked;
            this.creditPurchaseModalController.RetryClicked += OnRetryClicked;
        }

        public void Dispose()
        {
            creditPurchaseModalController.ModalOpened -= OnModalOpened;
            creditPurchaseModalController.BuyCreditsPrompted -= OnBuyCreditsPrompted;
            creditPurchaseModalController.PurchaseStarted -= OnPurchaseStarted;
            creditPurchaseModalController.PurchaseCompleted -= OnPurchaseCompleted;
            creditPurchaseModalController.PurchaseFailed -= OnPurchaseFailed;
            creditPurchaseModalController.PurchaseCancelled -= OnPurchaseCancelled;
            creditPurchaseModalController.NavigationClicked -= OnNavigationClicked;
            creditPurchaseModalController.RetryClicked -= OnRetryClicked;
        }

        private void OnModalOpened(ShopListingDto listing, string source)
        {
            JObject props = BuildItemProps(listing);
            props.Add("source", source);
            props.Add("price_credits", listing.priceCredits);
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_STARTED_CHECKOUT, props);
        }

        private void OnBuyCreditsPrompted(ShopListingDto listing, int missingCredits)
        {
            JObject props = BuildItemProps(listing);

            if (missingCredits >= 0)
                props.Add("missing_credits", missingCredits);

            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_PROMPTED, props);
        }

        private void OnPurchaseStarted(ShopListingDto listing, CreditsPurchaseQuote quote)
        {
            JObject props = BuildItemProps(listing);
            AddQuoteProps(props, quote);
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_STARTED_PURCHASE, props);
        }

        private void OnPurchaseCompleted(ShopListingDto listing, CreditsPurchaseQuote quote, string txHash, float durationSec)
        {
            double priceUsd = quote.UsdCents / 100.0;
            JObject props = BuildItemProps(listing);
            AddQuoteProps(props, quote);

            // Web-parity reconciliation shape (single-item purchase, so value == item price).
            props.Add("items", new JArray
            {
                new JObject
                {
                    { "item_id", listing.itemId },
                    { "contract_address", listing.contractAddress },
                    { "token_id", listing.tokenId },
                    { "price_usd", priceUsd },
                },
            });

            props.Add("value_credits", quote.Credits);
            props.Add("value_usd", priceUsd);
            props.Add("tx_hash", txHash);
            props.Add("duration_sec", durationSec);
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_COMPLETED_PURCHASE, props);
        }

        private void OnPurchaseFailed(ShopListingDto listing, string step, string errorCode, string errorDetail)
        {
            JObject props = BuildItemProps(listing);
            props.Add("step", step);
            props.Add("error_code", errorCode);
            props.Add("error_detail", errorDetail);
            props.Add("price_credits", listing.priceCredits);
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_PURCHASE_FAILED, props);
        }

        private void OnPurchaseCancelled(ShopListingDto listing, string stage)
        {
            JObject props = BuildItemProps(listing);
            props.Add("stage", stage);
            props.Add("price_credits", listing.priceCredits);
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_PURCHASE_CANCELLED, props);
        }

        private void OnNavigationClicked(ShopListingDto listing, string destination, string stage)
        {
            JObject props = BuildItemProps(listing);
            props.Add("destination", destination);
            props.Add("stage", stage);

            // The marketplace destination opens an external browser right after this and the app
            // may lose focus; a batched event could be lost.
            analytics.Track(AnalyticsEvents.MarketplaceCredits.CREDITS_PURCHASE_NAV_CLICKED, props,
                isInstant: destination == CreditPurchaseModalController.NAV_DESTINATION_MARKETPLACE);
        }

        private void OnRetryClicked(ShopListingDto listing) =>
            analytics.Track(AnalyticsEvents.MarketplaceCredits.CREDITS_PURCHASE_RETRY_CLICKED, BuildItemProps(listing));

        private static JObject BuildItemProps(ShopListingDto listing)
        {
            // Primary (creator mint) listings resolve by itemId and carry no tokenId; secondary
            // listings carry a tokenId — same rule the web shop uses for purchase_type.
            bool isPrimary = string.IsNullOrEmpty(listing.tokenId);

            return new JObject
            {
                { "trade_id", listing.tradeId },
                { "contract_address", listing.contractAddress },
                { "item_id", listing.itemId },
                { "token_id", listing.tokenId },
                { "category", listing.category },
                { "rarity", listing.rarity },
                { "creator", listing.creator },
                { "network", listing.network },
                { "chain_id", listing.chainId },
                { "purchase_type", isPrimary ? PURCHASE_TYPE_PRIMARY : PURCHASE_TYPE_RESALE },
                { "is_primary", isPrimary },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            };
        }

        private static void AddQuoteProps(JObject props, in CreditsPurchaseQuote quote)
        {
            props.Add("price_credits", quote.Credits);
            props.Add("price_usd", quote.UsdCents / 100.0);
            props.Add("is_live_rate", quote.IsLiveRatePrice);
        }
    }
}
