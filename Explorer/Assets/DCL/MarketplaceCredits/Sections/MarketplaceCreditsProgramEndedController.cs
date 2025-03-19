using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.UI;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedController : IDisposable
    {
        private const string SUBSCRIBE_LINK_ID = "SUBSCRIBE_LINK_ID";
        private const string X_LINK_ID = "X_LINK_ID";

        private readonly MarketplaceCreditsProgramEndedView view;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsProgramEndedController(
            MarketplaceCreditsProgramEndedView view,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.webBrowser = webBrowser;

            view.Subtitle.ConvertUrlsToClickeableLinks(OpenLink);
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

            view.Subtitle.text = $"Make sure to <color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> to find out when the next run goes live!";
        }

        public void Dispose() { }

        private void OpenLink(string id)
        {
            switch (id)
            {
                case SUBSCRIBE_LINK_ID:
                    webBrowser.OpenUrl(MarketplaceCreditsUtils.SUBSCRIBE_LINK);
                    break;
                case X_LINK_ID:
                    webBrowser.OpenUrl(MarketplaceCreditsUtils.X_LINK);
                    break;
            }
        }
    }
}
