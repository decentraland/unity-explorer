using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCreditsAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalsOfTheWeekController : IDisposable
    {
        private const string INFO_LINK = "https://docs.decentraland.org/";
        private const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace/";
        private const int GOALS_POOL_DEFAULT_CAPACITY = 4;

        private readonly MarketplaceCreditsGoalsOfTheWeekView view;
        private readonly IWebBrowser webBrowser;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IObjectPool<MarketplaceCreditsGoalRowView> goalRowsPool;
        private readonly List<MarketplaceCreditsGoalRowView> instantiatedGoalRows = new ();

        private Profile ownProfile;
        private CancellationTokenSource fetchGoalsOfTheWeekInfoCts;
        private CancellationTokenSource fetchCaptchaCts;
        private CancellationTokenSource claimCreditsCts;

        public MarketplaceCreditsGoalsOfTheWeekController(
            MarketplaceCreditsGoalsOfTheWeekView view,
            IWebBrowser webBrowser,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;

            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
            view.CaptchaControl.ReloadButton.onClick.AddListener(ReloadCaptcha);
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

        public void OnOpenSection()
        {
            fetchGoalsOfTheWeekInfoCts = fetchGoalsOfTheWeekInfoCts.SafeRestart();
            LoadGoalsOfTheWeekInfoAsync(fetchGoalsOfTheWeekInfoCts.Token).Forget();
        }

        public void Dispose()
        {
            view.InfoLinkButton.onClick.RemoveAllListeners();
            view.GoShoppingButton.onClick.RemoveAllListeners();
            view.CaptchaControl.ReloadButton.onClick.RemoveAllListeners();
            view.CaptchaControl.OnCaptchaSolved -= ClaimCredits;
            fetchGoalsOfTheWeekInfoCts.SafeCancelAndDispose();
            fetchCaptchaCts.SafeCancelAndDispose();
            claimCreditsCts.SafeCancelAndDispose();
        }

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(GO_SHOPPING_LINK);

        private MarketplaceCreditsGoalRowView InstantiateGoalRowPrefab()
        {
            MarketplaceCreditsGoalRowView goalRowView = Object.Instantiate(view.GoalRowPrefab, view.GoalsContainer);
            return goalRowView;
        }

        private async UniTaskVoid LoadGoalsOfTheWeekInfoAsync(CancellationToken ct)
        {
            try
            {
                view.SetAsLoading(true);
                view.CleanSection();
                ClearGoals();

                ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    var goalsOfTheWeekResponse = await marketplaceCreditsAPIClient.FetchGoalsOfTheWeekAsync(ownProfile.UserId, ct);
                    view.TimeLeftText.text = MarketplaceCreditsUtils.FormatEndOfTheWeekDateTimestamp(goalsOfTheWeekResponse.data.endOfTheWeekDate);
                    view.TotalCreditsText.text = MarketplaceCreditsUtils.FormatTotalCredits(goalsOfTheWeekResponse.data.totalCredits);

                    foreach (GoalData goalData in goalsOfTheWeekResponse.data.goals)
                    {
                        var goalRow = CreateAndSetupGoal(goalData);
                        instantiatedGoalRows.Add(goalRow);
                    }

                    view.ShowCaptcha(goalsOfTheWeekResponse.data.creditsAvailableToClaim);

                    if (goalsOfTheWeekResponse.data.creditsAvailableToClaim)
                        ReloadCaptcha();
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

        private MarketplaceCreditsGoalRowView CreateAndSetupGoal(GoalData goalData)
        {
            var goalRow = goalRowsPool.Get();

            goalRow.SetupGoalImage(goalData.thumbnail);
            goalRow.SetTitle(goalData.title);
            goalRow.SetCredits(goalData.credits);
            goalRow.SetAsCompleted(goalData.progress.stepsDone == goalData.progress.totalSteps);
            goalRow.SetAsPendingToClaim(goalData.progress.stepsDone == goalData.progress.totalSteps && !goalData.isClaimed);
            goalRow.SetProgress(goalData.progress.GetProgressPercentage(), goalData.progress.stepsDone, goalData.progress.totalSteps);

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

        private void ReloadCaptcha()
        {
            if (ownProfile == null)
                return;

            fetchCaptchaCts = fetchCaptchaCts.SafeRestart();
            LoadCaptchaAsync(ownProfile.UserId, fetchCaptchaCts.Token).Forget();
        }

        private async UniTaskVoid LoadCaptchaAsync(string walletId, CancellationToken ct)
        {
            try
            {
                view.SetCaptchaAsLoading(true);
                var captchaResponse = await marketplaceCreditsAPIClient.FetchCaptchaAsync(walletId, ct);
                view.SetCaptchaTargetAreaPercentageValue(captchaResponse.captchaValue);
                view.SetCaptchaAsLoading(false);
                view.SetCaptchaPercentageValue(0f);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the captcha. Please try again!";
                //marketplaceCreditsErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClaimCredits(float captchaValue)
        {
            if (ownProfile == null)
                return;

            claimCreditsCts = claimCreditsCts.SafeRestart();
            ClaimCreditsAsync(ownProfile.UserId, captchaValue, claimCreditsCts.Token).Forget();
        }

        private async UniTaskVoid ClaimCreditsAsync(string walletId, float captchaValue, CancellationToken ct)
        {
            try
            {
                view.SetCaptchaAsLoading(true);
                var claimCreditsResponse = await marketplaceCreditsAPIClient.ClaimCreditsAsync(walletId, captchaValue, ct);
                view.SetCaptchaAsLoading(false);

                if (claimCreditsResponse.success)
                {
                    // TODO (Santi): Show REWARD screen
                    // ...

                    OnOpenSection();
                }
                else
                    view.SetCaptchaAsErrorState(true);
            }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error claiming the credits. Please try again!";
                //marketplaceCreditsErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
