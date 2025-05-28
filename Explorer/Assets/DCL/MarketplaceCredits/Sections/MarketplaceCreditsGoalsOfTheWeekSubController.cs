using Cysharp.Threading.Tasks;
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
    public class MarketplaceCreditsGoalsOfTheWeekSubController : IDisposable
    {
        private const int GOALS_POOL_DEFAULT_CAPACITY = 4;

        public bool HasToPlayClaimCreditsAnimation { get; set; }

        private readonly MarketplaceCreditsGoalsOfTheWeekSubView subView;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly IObjectPool<MarketplaceCreditsGoalRowView> goalRowsPool;
        private readonly List<MarketplaceCreditsGoalRowView> instantiatedGoalRows = new ();
        private readonly MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView;
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;

        private CancellationTokenSource fetchCaptchaCts;
        private CancellationTokenSource claimCreditsCts;
        private CancellationTokenSource playClaimCreditsAnimationCts;

        public MarketplaceCreditsGoalsOfTheWeekSubController(
            MarketplaceCreditsGoalsOfTheWeekSubView subView,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            IWebRequestController webRequestController,
            MarketplaceCreditsTotalCreditsWidgetView totalCreditsWidgetView,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController)
        {
            this.subView = subView;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.totalCreditsWidgetView = totalCreditsWidgetView;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;

            marketplaceCreditsMenuController.OnAnyPlaceClick += CloseTimeLeftTooltip;
            subView.TimeLeftInfoButton.onClick.AddListener(ToggleTimeLeftTooltip);
            subView.CaptchaControl.ReloadFromNotLoadedStateButton.onClick.AddListener(ReloadCaptcha);
            subView.CaptchaControl.ReloadFromNotSolvedStateButton.onClick.AddListener(ReloadCaptcha);
            subView.CaptchaControl.OnCaptchaSolved += ClaimCredits;

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

        public void OpenSection()
        {
            subView.gameObject.SetActive(true);

            if (HasToPlayClaimCreditsAnimation)
            {
                playClaimCreditsAnimationCts = playClaimCreditsAnimationCts.SafeRestart();
                PlayClaimCreditsAnimationAsync(playClaimCreditsAnimationCts.Token).Forget();
                HasToPlayClaimCreditsAnimation = false;
            }
        }

        private async UniTaskVoid PlayClaimCreditsAnimationAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => totalCreditsWidgetView.TotalCreditsContainer.activeInHierarchy, cancellationToken: ct);
            totalCreditsWidgetView.PlayClaimCreditsAnimation();
        }

        public void CloseSection() =>
            subView.gameObject.SetActive(false);

        public void Setup(CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            ClearGoals();

            subView.SetTimeLeftText(MarketplaceCreditsUtils.FormatEndOfTheWeekDate(creditsProgramProgressResponse.currentWeek.timeLeft));
            subView.ShowTimeLeftTooltip(false);

            foreach (GoalData goalData in creditsProgramProgressResponse.goals)
            {
                var goalRow = CreateAndSetupGoal(goalData);
                instantiatedGoalRows.Add(goalRow);
            }

            subView.ShowCaptcha(creditsProgramProgressResponse.SomethingToClaim(), creditsProgramProgressResponse.credits.isBlockedForClaiming);

            if (creditsProgramProgressResponse.SomethingToClaim() && !creditsProgramProgressResponse.credits.isBlockedForClaiming)
                ReloadCaptcha();
        }

        public void Dispose()
        {
            marketplaceCreditsMenuController.OnAnyPlaceClick -= CloseTimeLeftTooltip;
            subView.TimeLeftInfoButton.onClick.RemoveListener(ToggleTimeLeftTooltip);
            subView.CaptchaControl.ReloadFromNotLoadedStateButton.onClick.RemoveListener(ReloadCaptcha);
            subView.CaptchaControl.ReloadFromNotSolvedStateButton.onClick.RemoveListener(ReloadCaptcha);
            subView.CaptchaControl.OnCaptchaSolved -= ClaimCredits;
            fetchCaptchaCts.SafeCancelAndDispose();
            claimCreditsCts.SafeCancelAndDispose();
            playClaimCreditsAnimationCts.SafeCancelAndDispose();
        }

        private MarketplaceCreditsGoalRowView InstantiateGoalRowPrefab()
        {
            MarketplaceCreditsGoalRowView goalRowView = Object.Instantiate(subView.GoalRowPrefab, subView.GoalsContainer);
            return goalRowView;
        }

        private MarketplaceCreditsGoalRowView CreateAndSetupGoal(GoalData goalData)
        {
            var goalRow = goalRowsPool.Get();

            goalRow.SetupGoalImage(goalData.thumbnail);
            goalRow.SetTitle(goalData.title);
            goalRow.SetCredits(MarketplaceCreditsUtils.FormatGoalReward(goalData.reward));
            goalRow.SetAsCompleted(goalData.progress.completedSteps == goalData.progress.totalSteps, goalData.isClaimed);
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

        private void ToggleTimeLeftTooltip() =>
            subView.ToggleTimeLeftTooltip();

        private void CloseTimeLeftTooltip() =>
            subView.ShowTimeLeftTooltip(false);

        private void ReloadCaptcha()
        {
            fetchCaptchaCts = fetchCaptchaCts.SafeRestart();
            LoadCaptchaAsync(fetchCaptchaCts.Token).Forget();
        }

        private async UniTaskVoid LoadCaptchaAsync(CancellationToken ct)
        {
            try
            {
                subView.SetCaptchaAsLoading(true);
                var captchaSprite = await marketplaceCreditsAPIClient.GenerateCaptchaAsync(ct);
                subView.SetCaptchaTargetAreaImage(captchaSprite);
                subView.SetCaptchaAsLoading(false);
                subView.SetCaptchaPercentageValue(0f);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                subView.SetCaptchaAsErrorState(true, isNonSolvedError: false);
                const string ERROR_MESSAGE = "There was an error loading the captcha. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClaimCredits(float captchaValue)
        {
            claimCreditsCts = claimCreditsCts.SafeRestart();
            ClaimCreditsAsync(captchaValue * 100, claimCreditsCts.Token).Forget();
        }

        private async UniTaskVoid ClaimCreditsAsync(float captchaValue, CancellationToken ct)
        {
            try
            {
                subView.SetCaptchaAsLoading(true);
                var claimCreditsResponse = await marketplaceCreditsAPIClient.ClaimCreditsAsync(captchaValue, ct);
                subView.SetCaptchaAsLoading(false);

                if (claimCreditsResponse.ok)
                {
                    marketplaceCreditsMenuController.ShowCreditsUnlockedPanelAsync(claimCreditsResponse.credits_granted).Forget();
                    marketplaceCreditsMenuController.SetSidebarButtonAnimationAsAlert(false);
                    marketplaceCreditsMenuController.SetSidebarButtonAsClaimIndicator(false);
                }
                else
                {
                    if (!claimCreditsResponse.isBlockedForClaiming)
                        subView.SetCaptchaAsErrorState(true, isNonSolvedError: true);
                    else
                        subView.ShowCaptcha(true, true);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                subView.SetCaptchaAsErrorState(true, isNonSolvedError: false);
                const string ERROR_MESSAGE = "There was an error claiming the credits. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
