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

            this.creditsTopUpModalController.BuyCreditsStarted += OnBuyCreditsStarted;
            this.creditsTopUpModalController.RedirectedToStripe += OnRedirectedToStripe;
            this.creditsTopUpModalController.BuyCreditsCompleted += OnBuyCreditsCompleted;
            this.creditsTopUpModalController.BuyCreditsPending += OnBuyCreditsPending;
            this.creditsTopUpModalController.BuyCreditsFailed += OnBuyCreditsFailed;
        }

        public void Dispose()
        {
            creditsTopUpModalController.BuyCreditsStarted -= OnBuyCreditsStarted;
            creditsTopUpModalController.RedirectedToStripe -= OnRedirectedToStripe;
            creditsTopUpModalController.BuyCreditsCompleted -= OnBuyCreditsCompleted;
            creditsTopUpModalController.BuyCreditsPending -= OnBuyCreditsPending;
            creditsTopUpModalController.BuyCreditsFailed -= OnBuyCreditsFailed;
        }

        private void OnBuyCreditsStarted(CreditPack pack, string source)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_STARTED_BUY_CREDITS, new JObject
            {
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { "provider", PROVIDER },
                { "source", source },
            });
        }

        private void OnRedirectedToStripe(string orderId, CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_REDIRECTED_TO_STRIPE, new JObject
            {
                { "order_id", orderId },
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
            });
        }

        private void OnBuyCreditsCompleted(string orderId, CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_COMPLETED_BUY_CREDITS, new JObject
            {
                { "order_id", orderId },
                { "pack_usd", pack.PriceUsd },
                { "credits", pack.Credits },
                { "provider", PROVIDER },
            });
        }

        private void OnBuyCreditsPending(CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_PENDING, new JObject
            {
                { "step", "grant" },
                { "pack_usd", pack.PriceUsd },
            });
        }

        private void OnBuyCreditsFailed(string step, string errorCode, CreditPack pack)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.SHOP_BUY_CREDITS_FAILED, new JObject
            {
                { "step", step },
                { "error_code", errorCode },
                { "pack_usd", pack.PriceUsd },
            });
        }
    }
}
