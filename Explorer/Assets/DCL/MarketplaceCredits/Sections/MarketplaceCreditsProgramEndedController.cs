using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.UI;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedController : IDisposable
    {
        private readonly MarketplaceCreditsProgramEndedView view;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsProgramEndedController(
            MarketplaceCreditsProgramEndedView view,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.webBrowser = webBrowser;

            view.Subtitle.ConvertUrlsToClickeableLinks(OpenUrl);
        }

        public void OpenSection() =>
            view.gameObject.SetActive(true);

        public void CloseSection() =>
            view.gameObject.SetActive(false);

        public void Setup(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            view.Title.text = !creditsProgramProgressResponse.season.isOutOfFunds ?
                $"The current run of Marketplace Credits Weekly Rewards ({MarketplaceCreditsUtils.FormatSeasonDateRange(creditsProgramProgressResponse.season.startDate, creditsProgramProgressResponse.season.endDate)}) has closed" :
                "All Available Credits Claimed: The beta run of the Weekly Rewards program is now closed";

            view.Subtitle.text = $"Make sure to <color=#FF2D55><b><u><link={MarketplaceCreditsUtils.SUBSCRIBE_LINK}>subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={MarketplaceCreditsUtils.X_LINK}>X</link></u></b></color> to find out when the next run goes live!";
        }

        public void Dispose() { }

        private void OpenUrl(string url) =>
            webBrowser.OpenUrl(url);
    }
}
