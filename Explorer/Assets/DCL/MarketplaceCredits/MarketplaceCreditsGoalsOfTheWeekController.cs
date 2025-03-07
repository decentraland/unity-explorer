using DCL.Browser;
using System;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalsOfTheWeekController : IDisposable
    {
        private const string INFO_LINK = "https://docs.decentraland.org/";
        private const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace/";

        private readonly MarketplaceCreditsGoalsOfTheWeekView view;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsGoalsOfTheWeekController(
            MarketplaceCreditsGoalsOfTheWeekView view,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.webBrowser = webBrowser;

            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
        }

        public void OnOpenSection()
        {

        }

        public void Dispose()
        {
            view.InfoLinkButton.onClick.RemoveAllListeners();
            view.GoShoppingButton.onClick.RemoveAllListeners();
        }

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(GO_SHOPPING_LINK);
    }
}
