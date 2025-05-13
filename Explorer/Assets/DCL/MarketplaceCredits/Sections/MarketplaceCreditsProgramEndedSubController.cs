using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedSubController : IDisposable
    {
        private const string SUBSCRIBE_LINK_ID = "SUBSCRIBE_LINK_ID";
        private const string X_LINK_ID = "X_LINK_ID";

        private readonly MarketplaceCreditsProgramEndedSubView subView;
        private readonly IWebBrowser webBrowser;

        public MarketplaceCreditsProgramEndedSubController(
            MarketplaceCreditsProgramEndedSubView subView,
            IWebBrowser webBrowser)
        {
            this.subView = subView;
            this.webBrowser = webBrowser;

            subView.Subtitle.ConvertUrlsToClickeableLinks(OpenLink);
        }

        public void OpenSection() =>
            subView.gameObject.SetActive(true);

        public void CloseSection() =>
            subView.gameObject.SetActive(false);

        public void Setup(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            string title = creditsProgramProgressResponse.season.seasonState switch
                           {
                               nameof(MarketplaceCreditsUtils.SeasonState.ENDED) => $"The current run of Marketplace Credits Weekly Rewards ({MarketplaceCreditsUtils.FormatSeasonDateRange(creditsProgramProgressResponse.season.startDate, creditsProgramProgressResponse.season.endDate)}) has closed",
                               nameof(MarketplaceCreditsUtils.SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS) => "All Available Credits Claimed: The beta run of the Weekly Rewards program is now closed",
                               nameof(MarketplaceCreditsUtils.SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS) => "All credits has been emitted for this week. Lets try next week",
                               nameof(MarketplaceCreditsUtils.SeasonState.ERR_PROGRAM_PAUSED) => "The Weekly Rewards program has been paused. Please come back later",
                               _ => string.Empty,
                           };

            subView.SetTitle(title);

            subView.SetSubtitle($"Make sure to <color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> to find out when the next run goes live!");
        }

        public void Dispose() { }

        private void OpenLink(string id)
        {
            switch (id)
            {
                case SUBSCRIBE_LINK_ID:
                    webBrowser.OpenUrl(DecentralandUrl.NewsletterSubscriptionLink);
                    break;
                case X_LINK_ID:
                    webBrowser.OpenUrl(DecentralandUrl.TwitterLink);
                    break;
            }

            subView.PlayOnLinkClickAudio();
        }
    }
}
