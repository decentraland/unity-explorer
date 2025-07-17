using DCL.Browser;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedSubController : IDisposable
    {
        // TODO: For now we hardcoded "the first Season" text, use "Season {0]" when number supplied from backend.
        private const string TITLE_CREDITS_VALID = "Time to go shopping—you've completed the first Season of Marketplace Credits!";
        private const string TITLE_CREDITS_VALID_PART2 = "Spend your Credits before they expire on {0}.";
        private const string TITLE_CREDITS_EXPIRED_NEXT_SEASON_KNOWN = "Save the date—Marketplace Credits Return for Season {0}!";
        private const string TITLE_CREDITS_EXPIRED_NEXT_SEASON_UNKNOWN = "The first Season of Marketplace Credits Has Closed";
        private const string TITLE_NO_FOUNDS_SEASON = "All Available Credits Claimed: The current Marketplace Credits Season is now closed";
        private const string TITLE_NO_FOUNDS_WEEK = "All Available Credits Claimed: The beta run of the Weekly Rewards program is now closed";
        private const string TITLE_MARKET_OFFLINE = "Marketplace Credits are Temporarily Offline";
        
        private const string SUBSCRIBE_LINK_ID = "SUBSCRIBE_LINK_ID";
        private const string X_LINK_ID = "X_LINK_ID";
        private const string DISCORD_LINK_ID = "DISCORD_LINK_ID";
        
        private readonly string subtitleCreditsValid = $"<color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>Subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> for news on the next season!";
        private readonly string subtitleSeasonCreditsRunOut = $"<color=#FF2D55><b><u><link={SUBSCRIBE_LINK_ID}>Subscribe</link></u></b></color> to Decentraland's newsletter or follow on <color=#FF2D55><b><u><link={X_LINK_ID}>X</link></u></b></color> for news on upcoming seasons";
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
            string boldedTitle = GetBoldedTitleText(creditsProgramProgressResponse);
            string normalTitle = GetNormalTitle(creditsProgramProgressResponse);
            string subTitle = GetSubtitleText(creditsProgramProgressResponse);

            subView.SetBoldTitle(boldedTitle);
            subView.SetNormalTitle(normalTitle);
            subView.SetSubtitle(subTitle);
        }

        public void Dispose() { }
        
        private string GetBoldedTitleText(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            // TODO: This variable will be provided from backend soon. Replace it when provided.
            uint nextSeasonAvailableInSeconds = 0;
            
            switch (creditsProgramProgressResponse.season.seasonState)
            {
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS):
                    return TITLE_NO_FOUNDS_SEASON;
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS):
                    return TITLE_NO_FOUNDS_WEEK;
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_PROGRAM_PAUSED):
                    return TITLE_MARKET_OFFLINE;
            }
            
            string seasonDate = MarketplaceCreditsUtils.FormatSeasonDateRange(creditsProgramProgressResponse.season.startDate, 
                creditsProgramProgressResponse.season.endDate);

            if (creditsProgramProgressResponse.credits.expiresIn > 0)
                return TITLE_CREDITS_VALID;

            // TODO: This variable will be provided from backend soon. Replace it when provided.
            string nextSeasonDate = seasonDate;
            
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (nextSeasonAvailableInSeconds > 0)
                return string.Format(TITLE_CREDITS_EXPIRED_NEXT_SEASON_KNOWN, nextSeasonDate);

            return TITLE_CREDITS_EXPIRED_NEXT_SEASON_UNKNOWN;
        }

        private string GetNormalTitle(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            if (creditsProgramProgressResponse.credits.expiresIn <= 0 ||
                creditsProgramProgressResponse.season.seasonState != nameof(MarketplaceCreditsUtils.SeasonState.ENDED))
                return string.Empty;
            
            string timeForCreditsToExpire = MarketplaceCreditsUtils.FormatSecondsToMonthDays(creditsProgramProgressResponse.credits.expiresIn);
            
            return string.Format(TITLE_CREDITS_VALID_PART2, timeForCreditsToExpire);
        }
        
        private string GetSubtitleText(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            // TODO: This variable will be provided from backend soon. Replace it when provided.
            uint nextSeasonAvailableInSeconds = 0;
            
            switch (creditsProgramProgressResponse.season.seasonState)
            {
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS):
                    return subtitleSeasonCreditsRunOut;
                case nameof(MarketplaceCreditsUtils.SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS):
                    return subtitleWeekCreditsRunOut;
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
