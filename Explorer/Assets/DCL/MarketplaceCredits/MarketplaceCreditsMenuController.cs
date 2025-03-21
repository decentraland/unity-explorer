using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Sections;
using DCL.MarketplaceCreditsAPIService;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using DCL.UI.Sidebar.SidebarActionsBus;
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
        private static readonly int SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID = Animator.StringToHash("isAlert");

        private readonly MarketplaceCreditsMenuView view;
        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly ISidebarBus sidebarBus;
        private readonly ISidebarActionsBus sidebarActionsBus;
        private readonly MarketplaceCreditsWelcomeController marketplaceCreditsWelcomeController;
        private readonly MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;
        private readonly MarketplaceCreditsWeekGoalsCompletedController marketplaceCreditsWeekGoalsCompletedController;
        private readonly MarketplaceCreditsProgramEndedController marketplaceCreditsProgramEndedController;
        private readonly IInputBlock inputBlock;
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
            ISidebarBus sidebarBus,
            ISidebarActionsBus sidebarActionsBus,
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
            this.sidebarBus = sidebarBus;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.sidebarActionsBus = sidebarActionsBus;
            this.sidebarCreditsButtonAnimator = sidebarCreditsButtonAnimator;
            this.sidebarCreditsButtonIndicator = sidebarCreditsButtonIndicator;

            mvcManager.OnViewClosed += OnCreditsUnlockedPanelClosed;
            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            notificationBusController.SubscribeToNotificationTypeReceived(NotificationType.MARKETPLACE_CREDITS, OnMarketplaceCreditsNotificationReceived);
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.MARKETPLACE_CREDITS, OnMarketplaceCreditsNotificationClicked);

            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(
                view.GoalsOfTheWeekView,
                webBrowser,
                marketplaceCreditsAPIClient,
                webRequestController,
                this);

            marketplaceCreditsWeekGoalsCompletedController = new MarketplaceCreditsWeekGoalsCompletedController(
                view.WeekGoalsCompletedView);

            marketplaceCreditsProgramEndedController = new MarketplaceCreditsProgramEndedController(
                view.ProgramEndedView,
                webBrowser);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(
                view.WelcomeView,
                view.TotalCreditsWidget,
                this,
                marketplaceCreditsGoalsOfTheWeekController,
                marketplaceCreditsWeekGoalsCompletedController,
                marketplaceCreditsProgramEndedController,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile);

            view.ErrorNotification.Hide(true, CancellationToken.None);
        }

        public void OpenPanel()
        {
            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.ShowAsync(showHideMenuCts.Token).Forget();
            OpenSection(MarketplaceCreditsSection.WELCOME);
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        public void ClosePanel()
        {
            if (!view.gameObject.activeSelf)
                return;

            sidebarBus.UnblockSidebar();
            showHideMenuCts = showHideMenuCts.SafeRestart();
            view.HideAsync(showHideMenuCts.Token).Forget();
            sidebarButton.Deselect();
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
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

            view.TotalCreditsWidget.gameObject.SetActive(section != MarketplaceCreditsSection.WELCOME);
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
            view.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenLearnMoreLink);

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveListener(ClosePanel);

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
            marketplaceCreditsWeekGoalsCompletedController.Dispose();
            marketplaceCreditsProgramEndedController.Dispose();
        }

        private void CloseAllSections()
        {
            marketplaceCreditsWelcomeController.CloseSection();
            marketplaceCreditsGoalsOfTheWeekController.CloseSection();
            marketplaceCreditsWeekGoalsCompletedController.CloseSection();
            marketplaceCreditsProgramEndedController.CloseSection();
        }

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
            SetSidebarCreditsButtonAlertAnimation(true);
            SetSidebarCreditsButtonAlertClaimIndicator(true);
        }

        private void OnMarketplaceCreditsNotificationClicked(object[] parameters)
        {
            sidebarActionsBus.CloseAllWidgets();
            OpenPanel();
        }

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
                        SetSidebarCreditsButtonAlertAnimation(false);
                        SetSidebarCreditsButtonAlertClaimIndicator(false);
                        return;
                    }

                    bool thereIsSomethingToClaim = creditsProgramProgressResponse.SomethingToClaim();
                    SetSidebarCreditsButtonAlertAnimation(!creditsProgramProgressResponse.user.isRegistered || thereIsSomethingToClaim);
                    SetSidebarCreditsButtonAlertClaimIndicator(creditsProgramProgressResponse.user.isRegistered && thereIsSomethingToClaim);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading the Credits Program. Please try again!";
                ReportHub.LogError(ReportCategory.MARKETPLACE_CREDITS, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        public void SetSidebarCreditsButtonAlertAnimation(bool isOn) =>
            sidebarCreditsButtonAnimator.SetBool(SIDEBAR_BUTTON_ANIMATOR_IS_ALERT_ID, isOn);

        public void SetSidebarCreditsButtonAlertClaimIndicator(bool isOn) =>
            sidebarCreditsButtonIndicator.SetActive(isOn);
    }
}
