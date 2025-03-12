using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles.Self;
using System;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsWeekGoalsCompletedController : IDisposable
    {
        private readonly MarketplaceCreditsWeekGoalsCompletedView view;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;

        public MarketplaceCreditsWeekGoalsCompletedController(
            MarketplaceCreditsWeekGoalsCompletedView view,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;

            view.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
        }

        public void OnOpenSection() { }

        public void Setup(float totalCredits, string endOfTheWeekDate)
        {
            view.TotalCreditsWidget.SetCredits(MarketplaceCreditsUtils.FormatTotalCredits(totalCredits));
            view.TimeLeftText.text = MarketplaceCreditsUtils.FormatEndOfTheWeekDateTimestamp(endOfTheWeekDate);
        }

        public void Dispose() =>
            view.TotalCreditsWidget.GoShoppingButton.onClick.RemoveAllListeners();

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.GO_SHOPPING_LINK);
    }
}
