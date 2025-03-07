using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles.Self;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalsOfTheWeekController : IDisposable
    {
        private const string INFO_LINK = "https://docs.decentraland.org/";
        private const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace/";

        private readonly MarketplaceCreditsGoalsOfTheWeekView view;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;

        private CancellationTokenSource fetchGoalsOfTheWeekInfoCts;

        public MarketplaceCreditsGoalsOfTheWeekController(
            MarketplaceCreditsGoalsOfTheWeekView view,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;

            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
        }

        public void OnOpenSection()
        {
            fetchGoalsOfTheWeekInfoCts = fetchGoalsOfTheWeekInfoCts.SafeRestart();
            LoadGoalsOfTheWeekInfoAsync(fetchGoalsOfTheWeekInfoCts.Token).Forget();
        }

        public void Dispose()
        {
            view.InfoLinkButton.onClick.RemoveAllListeners();
            view.GoShoppingButton.onClick.RemoveAllListeners();
            fetchGoalsOfTheWeekInfoCts.SafeCancelAndDispose();
        }

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(GO_SHOPPING_LINK);

        private async UniTaskVoid LoadGoalsOfTheWeekInfoAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);
                view.CleanSection();

                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    var goalsOfTheWeekResponse = await marketplaceCreditsAPIClient.FetchGoalsOfTheWeekAsync(ownProfile.UserId, ct);
                    view.TimeLeftText.text = MarketplaceCreditsUtils.FormatEndOfTheWeekDateTimestamp(goalsOfTheWeekResponse.data.endOfTheWeekDate);
                    view.TotalCreditsText.text = MarketplaceCreditsUtils.FormatTotalCredits(goalsOfTheWeekResponse.data.totalCredits);
                    view.ShowCaptcha(goalsOfTheWeekResponse.data.creditsAvailableToClaim);
                }

                view.SetAsLoading(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the goals of the week. Please try again!";
                //marketplaceCreditsErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
