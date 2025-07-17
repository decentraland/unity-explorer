using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedSubController : IDisposable
    {
        private const string TITLE_CREDITS_VALID = "Time to go shopping—you've completed Season {0} of Marketplace Credits!\nSpend your Credits before they expire on {1}.";
        private const string TITLE_CREDITS_EXPIRED_NEXT_SEASON_KNOWN = "Save the date—Marketplace Credits Return for Season {0}!";
        private const string TITLE_CREDITS_EXPIRED_NEXT_SEASON_UNKNOWN = "Season {0} of Marketplace Credits Has Closed.";
        private const string TITLE_NO_FOUNDS = "All Available Credits Claimed: The beta run of the Weekly Rewards program is now closed";
        private const string TITLE_MARKET_OFFLINE = "Marketplace Credits are Temporarily Offline";
        
        private const string SUBSCRIBE_LINK_ID = "SUBSCRIBE_LINK_ID";
        private const string X_LINK_ID = "X_LINK_ID";
        private const string DISCORD_LINK_ID = "DISCORD_LINK_ID";
        
        private readonly string subtitleCreditsValid = $"<color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>Subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> for news on the next season!";
        private readonly string subtitleWeekCreditsRunOut = $"Make sure to <color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> to find out when the next run goes live!";
        private readonly string subtitleCreditsExpiredNextSeasonKnown = $"<color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>Subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> for more news.";
        private readonly string subtitleCreditsExpiredNextSeasonUnknown = $"<color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>Subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> for news on the next season's start date!";
        private readonly string subtitleMarketOffline = $"Please check the #product-status channel in <color=#FF2D55><b><u><link={DISCORD_LINK_ID}>Decentraland's Discord server</link></u></b></color> for updates if service does not resume shortly.";

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
            string title = GetTitleText(creditsProgramProgressResponse);
            string subTitle = GetSubtitleText(creditsProgramProgressResponse);

            subView.SetTitle(title);
            subView.SetSubtitle(subTitle);
        }

        public void Dispose() { }
        
        private string GetTitleText(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            // TODO: This variable will be provided from backend soon. Replace it when provided.
            uint nextSeasonAvailableInSeconds = 0;
            
            switch (creditsProgramProgressResponse.season.seasonState)
            {
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS):
                    return TITLE_NO_FOUNDS;
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS):
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_PROGRAM_PAUSED):
                    return TITLE_MARKET_OFFLINE;
            }
            
            string seasonDate = MarketplaceCreditsUtils.FormatSeasonDateRange(creditsProgramProgressResponse.season.startDate, 
                creditsProgramProgressResponse.season.endDate);
            string timeForCreditsToExpire = MarketplaceCreditsUtils.FormatSecondsToMonthDays(creditsProgramProgressResponse.credits.expiresIn);

            if (creditsProgramProgressResponse.credits.expiresIn > 0)
                return string.Format(TITLE_CREDITS_VALID, seasonDate, timeForCreditsToExpire);

            // TODO: This variable will be provided from backend soon. Replace it when provided.
            string nextSeasonDate = seasonDate;
            
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (nextSeasonAvailableInSeconds > 0)
                return String.Format(TITLE_CREDITS_EXPIRED_NEXT_SEASON_KNOWN, nextSeasonDate);

            return String.Format(TITLE_CREDITS_EXPIRED_NEXT_SEASON_UNKNOWN, seasonDate);
        }
        
        private string GetSubtitleText(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            // TODO: This variable will be provided from backend soon. Replace it when provided.
            uint nextSeasonAvailableInSeconds = 0;
            
            switch (creditsProgramProgressResponse.season.seasonState)
            {
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS):
                    return subtitleWeekCreditsRunOut;
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS):
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_PROGRAM_PAUSED):
                    return subtitleMarketOffline;
            }
            
            if (creditsProgramProgressResponse.credits.expiresIn > 0)
                return subtitleCreditsValid;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (nextSeasonAvailableInSeconds > 0)
                return subtitleCreditsExpiredNextSeasonKnown;

            return subtitleCreditsExpiredNextSeasonUnknown;
        }

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
                case DISCORD_LINK_ID:
                    webBrowser.OpenUrl(DecentralandUrl.DiscordDirectLink);
                    break;
            }

            subView.PlayOnLinkClickAudio();
        }
    }
}
