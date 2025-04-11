using DCL.MarketplaceCredits;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class MarketplaceCreditsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        public MarketplaceCreditsAnalytics(
            IAnalyticsController analytics,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.analytics = analytics;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;

            this.marketplaceCreditsMenuController.MarketplaceCreditsOpened += OnMarketplaceCreditsOpened;
        }

        public void Dispose() =>
            marketplaceCreditsMenuController.MarketplaceCreditsOpened -= OnMarketplaceCreditsOpened;

        private void OnMarketplaceCreditsOpened(bool openedFromNotification)
        {
            analytics.Track(AnalyticsEvents.MarketplaceCredits.MARKETPLACE_CREDITS_OPENED, new JsonObject
            {
                { "from_notification", openedFromNotification },
            });
        }
    }
}
