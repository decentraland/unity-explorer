using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class CreditsTopUpAnalytics : IDisposable
    {
        private const string PROVIDER = "stripe";

        private readonly IAnalyticsController analytics;
        private readonly CreditsTopUpModalController creditsTopUpModalController;

        public CreditsTopUpAnalytics(
            IAnalyticsController analytics,
            CreditsTopUpModalController creditsTopUpModalController)
        {
            this.analytics = analytics;
            this.creditsTopUpModalController = creditsTopUpModalController;

            this.creditsTopUpModalController.ModalOpened += OnModalOpened;
            this.creditsTopUpModalController.BuyCreditsStarted += OnBuyCreditsStarted;
            this.creditsTopUpModalController.RedirectedToStripe += OnRedirectedToStripe;
            this.creditsTopUpModalController.BuyCreditsCompleted += OnBuyCreditsCompleted;
            this.creditsTopUpModalController.BuyCreditsPending += OnBuyCreditsPending;
            this.creditsTopUpModalController.BuyCreditsFailed += OnBuyCreditsFailed;
            this.creditsTopUpModalController.BuyCreditsCancelled += OnBuyCreditsCancelled;
            this.creditsTopUpModalController.RetryClicked += OnRetryClicked;
            this.creditsTopUpModalController.PacksLoadFailed += OnPacksLoadFailed;
        }

        public void Dispose()
        {
            creditsTopUpModalController.ModalOpened -= OnModalOpened;
            creditsTopUpModalController.BuyCreditsStarted -= OnBuyCreditsStarted;
            creditsTopUpModalController.RedirectedToStripe -= OnRedirectedToStripe;
            creditsTopUpModalController.BuyCreditsCompleted -= OnBuyCreditsCompleted;
            creditsTopUpModalController.BuyCreditsPending -= OnBuyCreditsPending;
            creditsTopUpModalController.BuyCreditsFailed -= OnBuyCreditsFailed;
            creditsTopUpModalController.BuyCreditsCancelled -= OnBuyCreditsCancelled;
            creditsTopUpModalController.RetryClicked -= OnRetryClicked;
            creditsTopUpModalController.PacksLoadFailed -= OnPacksLoadFailed;
        }

        private void OnModalOpened(string source)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.CREDITS_TOPUP_OPENED, new JObject
            {
                { "source", source },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnBuyCreditsStarted(CreditPack pack, string source)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_STARTED_BUY_CREDITS, new JObject
            {
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { "provider", PROVIDER },
                { "source", source },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnRedirectedToStripe(string orderId, CreditPack pack)
        {
            // Instant: the external browser opens right after this and the app may lose focus or be
            // killed while the user pays; a batched event could be lost.
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_REDIRECTED_TO_STRIPE, new JObject
            {
                { "order_id", orderId },
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            }, isInstant: true);
        }

        private void OnBuyCreditsCompleted(string orderId, CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_COMPLETED_BUY_CREDITS, new JObject
            {
                { "order_id", orderId },
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { "provider", PROVIDER },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnBuyCreditsPending(CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_PENDING, new JObject
            {
                { "step", "grant" },
                { "pack_usd", pack.PriceUsd },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnBuyCreditsFailed(string step, string errorCode, CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_FAILED, new JObject
            {
                { "step", step },
                { "error_code", errorCode },
                { "pack_usd", pack.PriceUsd },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnBuyCreditsCancelled(string orderId, CreditPack pack)
        {
            // Soft cancel: the user stopped waiting for the Stripe browser, but the order is handed
            // to the background poll, so a later "Shop Completed Buy Credits" is still legitimate.
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_CANCELLED, new JObject
            {
                { "order_id", orderId },
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { "provider", PROVIDER },
                { "mode", "background_handoff" },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnRetryClicked(CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.CREDITS_TOPUP_RETRY_CLICKED, new JObject
            {
                { "pack_usd", pack.PriceUsd },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }

        private void OnPacksLoadFailed(string reason)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.CREDITS_TOPUP_PACKS_LOAD_FAILED, new JObject
            {
                { "reason", reason },
                { AnalyticsEvents.MarketplaceCredits.PLATFORM_KEY, AnalyticsEvents.MarketplaceCredits.PLATFORM_VALUE },
            });
        }
    }
}
