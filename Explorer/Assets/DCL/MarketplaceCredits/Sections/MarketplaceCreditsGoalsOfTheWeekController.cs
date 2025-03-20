using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCreditsAPIService;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsGoalsOfTheWeekController : IDisposable
    {
        private const int GOALS_POOL_DEFAULT_CAPACITY = 4;

        private readonly MarketplaceCreditsGoalsOfTheWeekView view;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly IObjectPool<MarketplaceCreditsGoalRowView> goalRowsPool;
        private readonly List<MarketplaceCreditsGoalRowView> instantiatedGoalRows = new ();
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        private string currentWalletId;
        private CancellationTokenSource fetchCaptchaCts;
        private CancellationTokenSource claimCreditsCts;

        public MarketplaceCreditsGoalsOfTheWeekController(
            MarketplaceCreditsGoalsOfTheWeekView view,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWebRequestController webRequestController,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.view = view;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;

            view.TimeLeftLinkButton.onClick.AddListener(OpenTimeLeftInfoLink);
            view.CaptchaControl.ReloadFromNotLoadedStateButton.onClick.AddListener(ReloadCaptcha);
            view.CaptchaControl.ReloadFromNotSolvedStateButton.onClick.AddListener(ReloadCaptcha);
            view.CaptchaControl.OnCaptchaSolved += ClaimCredits;

            goalRowsPool = new ObjectPool<MarketplaceCreditsGoalRowView>(
                InstantiateGoalRowPrefab,
                defaultCapacity: GOALS_POOL_DEFAULT_CAPACITY,
                actionOnGet: goalRowView =>
                {
                    goalRowView.ConfigureImageController(webRequestController);
                    goalRowView.gameObject.SetActive(true);
                    goalRowView.transform.SetAsLastSibling();
                },
                actionOnRelease: goalRowView => goalRowView.gameObject.SetActive(false));
        }

        public void OpenSection() =>
            view.gameObject.SetActive(true);

        public void CloseSection() =>
            view.gameObject.SetActive(false);

        public void Setup(string walletId, CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            currentWalletId = walletId;

            ClearGoals();

            view.TimeLeftText.text = MarketplaceCreditsUtils.FormatEndOfTheWeekDate(creditsProgramProgressResponse.currentWeek.timeLeft);

            foreach (GoalData goalData in creditsProgramProgressResponse.goals)
            {
                var goalRow = CreateAndSetupGoal(goalData);
                instantiatedGoalRows.Add(goalRow);
            }

            view.ShowCaptcha(creditsProgramProgressResponse.SomethingToClaim());

            if (creditsProgramProgressResponse.SomethingToClaim())
                ReloadCaptcha();
        }

        public void Dispose()
        {
            view.TimeLeftLinkButton.onClick.RemoveListener(OpenTimeLeftInfoLink);
            view.CaptchaControl.ReloadFromNotLoadedStateButton.onClick.RemoveListener(ReloadCaptcha);
            view.CaptchaControl.ReloadFromNotSolvedStateButton.onClick.RemoveListener(ReloadCaptcha);
            view.CaptchaControl.OnCaptchaSolved -= ClaimCredits;
            fetchCaptchaCts.SafeCancelAndDispose();
            claimCreditsCts.SafeCancelAndDispose();
        }

        private MarketplaceCreditsGoalRowView InstantiateGoalRowPrefab()
        {
            MarketplaceCreditsGoalRowView goalRowView = Object.Instantiate(view.GoalRowPrefab, view.GoalsContainer);
            return goalRowView;
        }

        private MarketplaceCreditsGoalRowView CreateAndSetupGoal(GoalData goalData)
        {
            var goalRow = goalRowsPool.Get();

            goalRow.SetupGoalImage(goalData.thumbnail);
            goalRow.SetTitle(goalData.title);
            goalRow.SetCredits(MarketplaceCreditsUtils.FormatGoalReward(goalData.reward));
            goalRow.SetAsCompleted(goalData.progress.completedSteps == goalData.progress.totalSteps);
            goalRow.SetClaimStatus(goalData.progress.completedSteps == goalData.progress.totalSteps && !goalData.isClaimed, goalData.isClaimed);
            goalRow.SetProgress(goalData.progress.GetProgressPercentage(), goalData.progress.completedSteps, goalData.progress.totalSteps);

            return goalRow;
        }

        private void ClearGoals()
        {
            foreach (var goalRow in instantiatedGoalRows)
            {
                goalRow.StopLoadingImage();
                goalRowsPool.Release(goalRow);
            }

            instantiatedGoalRows.Clear();
        }

        private void OpenTimeLeftInfoLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.TIME_LEFT_INFO_LINK);

        private void ReloadCaptcha()
        {
            fetchCaptchaCts = fetchCaptchaCts.SafeRestart();
            LoadCaptchaAsync(currentWalletId, fetchCaptchaCts.Token).Forget();
        }

        private async UniTaskVoid LoadCaptchaAsync(string walletId, CancellationToken ct)
        {
            try
            {
                view.SetCaptchaAsLoading(true);
                var captchaResponse = await marketplaceCreditsAPIClient.GenerateCaptchaAsync(walletId, ct);
                view.SetCaptchaTargetAreaPercentageValue(captchaResponse.captchaValue);
                view.SetCaptchaAsLoading(false);
                view.SetCaptchaPercentageValue(0f);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.SetCaptchaAsErrorState(true, isNonSolvedError: false);
                const string ERROR_MESSAGE = "There was an error loading the captcha. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClaimCredits(float captchaValue)
        {
            claimCreditsCts = claimCreditsCts.SafeRestart();
            ClaimCreditsAsync(currentWalletId, captchaValue, claimCreditsCts.Token).Forget();
        }

        private async UniTaskVoid ClaimCreditsAsync(string walletId, float captchaValue, CancellationToken ct)
        {
            try
            {
                view.SetCaptchaAsLoading(true);
                var claimCreditsResponse = await marketplaceCreditsAPIClient.ClaimCreditsAsync(walletId, captchaValue, ct);
                view.SetCaptchaAsLoading(false);

                if (claimCreditsResponse.success)
                    marketplaceCreditsMenuController.ShowCreditsUnlockedPanel(claimCreditsResponse.claimedCredits);
                else
                    view.SetCaptchaAsErrorState(true, isNonSolvedError: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.SetCaptchaAsErrorState(true, isNonSolvedError: false);
                const string ERROR_MESSAGE = "There was an error claiming the credits. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
