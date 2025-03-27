using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.MarketplaceCredits.Sections;
using DCL.MarketplaceCreditsAPIService;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Buttons;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuController : IDisposable
    {
        public event Action OnAnyPlaceClick;

        private static readonly int SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID = Animator.StringToHash("isAlert");
        private static readonly int SIDEBAR_BUTTON_ANIMATOR_IS_PAUSED_ID = Animator.StringToHash("isPaused");

        private readonly MarketplaceCreditsMenuView view;
        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly MarketplaceCreditsWelcomeController marketplaceCreditsWelcomeController;
        private readonly MarketplaceCreditsVerifyEmailController marketplaceCreditsVerifyEmailController;
        private readonly MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;
        private readonly MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController;
        private readonly MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly Animator sidebarCreditsButtonAnimator;
        private readonly GameObject sidebarCreditsButtonIndicator;

        private CancellationTokenSource showHideMenuCts;
        private CancellationTokenSource showCreditsUnlockedCts;
        private CancellationTokenSource showErrorNotificationCts;
        private CancellationTokenSource sidebarButtonStateCts;

        private Profile ownProfile;

        public MarketplaceCreditsMenuController(
            MarketplaceCreditsMenuView view,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            INotificationsBusController notificationBusController,
            Animator sidebarCreditsButtonAnimator,
            GameObject sidebarCreditsButtonIndicator)
        {
            this.sidebarButton = sidebarButton;
            this.view = view;
            this.webBrowser = webBrowser;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.sidebarCreditsButtonAnimator = sidebarCreditsButtonAnimator;
            this.sidebarCreditsButtonIndicator = sidebarCreditsButtonIndicator;

            mvcManager.OnViewClosed += OnCreditsUnlockedPanelClosed;
            view.OnAnyPlaceClick += OnAnyPlaceClicked;
            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.MARKETPLACE_CREDITS, OnMarketplaceCreditsNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.MARKETPLACE_CREDITS, OnMarketplaceCreditsNotificationClicked);

            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(
                view.GoalsOfTheWeekView,
                marketplaceCreditsAPIClient,
                webRequestController,
                this);

            marketplaceCreditsWeekGoalsCompletedController = new MarketplaceCreditsWeekGoalsCompletedController(
                view.WeekGoalsCompletedView);

            marketplaceCreditsProgramEndedController = new MarketplaceCreditsProgramEndedController(
                view.ProgramEndedView,
                webBrowser);

            marketplaceCreditsVerifyEmailController = new MarketplaceCreditsVerifyEmailController(
                view.VerifyEmailView,
                selfProfile,
                marketplaceCreditsAPIClient,
                this);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(
                view.WelcomeView,
                view.TotalCreditsWidget,
                this,
                marketplaceCreditsVerifyEmailController,
                marketplaceCreditsGoalsOfTheWeekController,
                marketplaceCreditsWeekGoalsCompletedController,
                marketplaceCreditsProgramEndedController,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile,
                inputBlock);

            view.ErrorNotification.Hide(true, CancellationToken.None);
        }

        public void OpenPanel()
        {
            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.ShowAsync(showHideMenuCts.Token).Forget();
            OpenSection(MarketplaceCreditsSection.WELCOME);
            SetSidebarButtonAnimationAsPaused(true);
        }

        public void ClosePanel()
        {
            if (!view.gameObject.activeSelf)
                return;

            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.HideAsync(showHideMenuCts.Token).Forget();
            sidebarButton.Deselect();
            CloseAllSections();
            SetSidebarButtonAnimationAsPaused(false);
        }

        public void OpenSection(MarketplaceCreditsSection section)
        {
            CloseAllSections();

            view.TotalCreditsWidget.SetAsProgramEndVersion(isProgramEndVersion: false);

            switch (section)
            {
                case MarketplaceCreditsSection.WELCOME:
                    marketplaceCreditsWelcomeController.OpenSection();
                    break;
                case MarketplaceCreditsSection.VERIFY_EMAIL:
                    marketplaceCreditsVerifyEmailController.OpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    marketplaceCreditsGoalsOfTheWeekController.OpenSection();
                    break;
                case MarketplaceCreditsSection.WEEK_GOALS_COMPLETED:
                    marketplaceCreditsWeekGoalsCompletedController.OpenSection();
                    break;
                case MarketplaceCreditsSection.PROGRAM_ENDED:
                    view.TotalCreditsWidget.SetAsProgramEndVersion(isProgramEndVersion: true);
                    marketplaceCreditsProgramEndedController.OpenSection();
                    break;
            }

            view.TotalCreditsWidget.gameObject.SetActive(section != MarketplaceCreditsSection.WELCOME && section != MarketplaceCreditsSection.VERIFY_EMAIL);
        }

        public void ShowCreditsUnlockedPanel(float claimedCredits)
        {
            showCreditsUnlockedCts = showCreditsUnlockedCts.SafeRestart();
            mvcManager.ShowAsync(CreditsUnlockedController.IssueCommand(new CreditsUnlockedController.Params(claimedCredits)), showCreditsUnlockedCts.Token).Forget();
        }

        public void ShowErrorNotification(string message)
        {
            showErrorNotificationCts = showErrorNotificationCts.SafeRestart();
            ShowErrorNotificationAsync(message, showErrorNotificationCts.Token).Forget();
        }

        public void CheckForSidebarButtonState()
        {
            sidebarButtonStateCts = sidebarButtonStateCts.SafeRestart();
            CheckForSidebarButtonStateAsync(sidebarButtonStateCts.Token).Forget();
        }

        public void Dispose()
        {
            showHideMenuCts.SafeCancelAndDispose();
            showCreditsUnlockedCts.SafeCancelAndDispose();
            showErrorNotificationCts.SafeCancelAndDispose();
            sidebarButtonStateCts.SafeCancelAndDispose();

            mvcManager.OnViewClosed -= OnCreditsUnlockedPanelClosed;
            view.OnAnyPlaceClick -= OnAnyPlaceClicked;
            view.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenLearnMoreLink);

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveListener(ClosePanel);

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsVerifyEmailController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
            marketplaceCreditsWeekGoalsCompletedController.Dispose();
            marketplaceCreditsProgramEndedController.Dispose();
        }

        private void CloseAllSections()
        {
            marketplaceCreditsWelcomeController.CloseSection();
            marketplaceCreditsVerifyEmailController.CloseSection();
            marketplaceCreditsGoalsOfTheWeekController.CloseSection();
            marketplaceCreditsWeekGoalsCompletedController.CloseSection();
            marketplaceCreditsProgramEndedController.CloseSection();
        }

        private void OnAnyPlaceClicked() =>
            OnAnyPlaceClick?.Invoke();

        private void OpenInfoLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.WEEKLY_REWARDS_INFO_LINK);

        private void OpenLearnMoreLink() =>
            webBrowser.OpenUrl(MarketplaceCreditsUtils.GO_SHOPPING_LINK);

        private async UniTaskVoid ShowErrorNotificationAsync(string message, CancellationToken ct)
        {
            view.ErrorNotification.SetText(message);
            view.ErrorNotification.Show(ct);
            await UniTask.Delay(MarketplaceCreditsUtils.ERROR_NOTIFICATION_DURATION * 1000, cancellationToken: ct);
            view.ErrorNotification.Hide(false, ct);
        }

        private void OnCreditsUnlockedPanelClosed(IController controller)
        {
            if (controller is not CreditsUnlockedController)
                return;

            OpenSection(MarketplaceCreditsSection.WELCOME);
        }

        private void OnMarketplaceCreditsNotificationReceived(INotification notification)
        {
            SetSidebarButtonAnimationAsAlert(true);
            SetSidebarButtonAsClaimIndicator(true);
        }

        private void OnMarketplaceCreditsNotificationClicked(object[] parameters) =>
            OpenPanel();

        private async UniTaskVoid CheckForSidebarButtonStateAsync(CancellationToken ct)
        {
            try
            {
                ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null)
                {
                    var creditsProgramProgressResponse = await marketplaceCreditsAPIClient.GetProgramProgressAsync(ownProfile.UserId, ct);

                    if (creditsProgramProgressResponse.season.timeLeft <= 0f || creditsProgramProgressResponse.season.isOutOfFunds)
                    {
                        SetSidebarButtonAnimationAsAlert(false);
                        SetSidebarButtonAsClaimIndicator(false);
                        return;
                    }

                    bool thereIsSomethingToClaim = creditsProgramProgressResponse.SomethingToClaim();
                    SetSidebarButtonAnimationAsAlert(!creditsProgramProgressResponse.IsUserEmailVerified() || thereIsSomethingToClaim);
                    SetSidebarButtonAsClaimIndicator(creditsProgramProgressResponse.IsUserEmailVerified() && thereIsSomethingToClaim);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the Credits Program. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        public void SetSidebarButtonAnimationAsAlert(bool isOn) =>
            sidebarCreditsButtonAnimator.SetBool(SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID, isOn);

        public void SetSidebarButtonAsClaimIndicator(bool isOn) =>
            sidebarCreditsButtonIndicator.SetActive(isOn);

        private void SetSidebarButtonAnimationAsPaused(bool isOn) =>
            sidebarCreditsButtonAnimator.SetBool(SIDEBAR_BUTTON_ANIMATOR_IS_PAUSED_ID, isOn);
    }
}
