using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Sections;
using DCL.MarketplaceCreditsAPIService;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles.Self;
using DCL.SidebarBus;
using DCL.UI.Buttons;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuController : IDisposable
    {
        private readonly MarketplaceCreditsMenuView view;
        private readonly HoverableAndSelectableButtonWithAnimator sidebarButton;
        private readonly ISidebarBus sidebarBus;
        private readonly MarketplaceCreditsWelcomeController marketplaceCreditsWelcomeController;
        private readonly MarketplaceCreditsGoalsOfTheWeekController marketplaceCreditsGoalsOfTheWeekController;
        private readonly IInputBlock inputBlock;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly INotificationsBusController notificationBusController;

        private CancellationTokenSource showHideMenuCts;
        private CancellationTokenSource showCreditsUnlockedCts;
        private CancellationTokenSource showErrorNotificationCts;

        public MarketplaceCreditsMenuController(
            MarketplaceCreditsMenuView view,
            HoverableAndSelectableButtonWithAnimator sidebarButton,
            ISidebarBus sidebarBus,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            INotificationsBusController notificationBusController)
        {
            this.sidebarButton = sidebarButton;
            this.view = view;
            this.sidebarBus = sidebarBus;
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.mvcManager = mvcManager;
            this.notificationBusController = notificationBusController;

            mvcManager.OnViewClosed += OnCreditsUnlockedPanelClosed;
            view.InfoLinkButton.onClick.AddListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.AddListener(OpenLearnMoreLink);
            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.AddListener(ClosePanel);

            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.MARKETPLACE_CREDITS, OnMarketplaceCreditsNotificationClicked);

            marketplaceCreditsWelcomeController = new MarketplaceCreditsWelcomeController(
                view.WelcomeView,
                view.TotalCreditsWidget,
                view.WeekGoalsCompletedView,
                this,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile);

            marketplaceCreditsGoalsOfTheWeekController = new MarketplaceCreditsGoalsOfTheWeekController(
                view.GoalsOfTheWeekView,
                view.TotalCreditsWidget,
                webBrowser,
                marketplaceCreditsAPIClient,
                selfProfile,
                webRequestController,
                this);

            view.ErrorNotification.Hide(true, CancellationToken.None);
        }

        public void OpenPanel(bool forceReopen = false)
        {
            if (!forceReopen && view.gameObject.activeSelf)
                return;

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

            TestNotificationAsync(CancellationToken.None).Forget();
        }

        public void OpenSection(MarketplaceCreditsSection section)
        {
            CloseAllSections();

            switch (section)
            {
                case MarketplaceCreditsSection.WELCOME:
                    view.WelcomeView.gameObject.SetActive(true);
                    marketplaceCreditsWelcomeController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.GOALS_OF_THE_WEEK:
                    view.GoalsOfTheWeekView.gameObject.SetActive(true);
                    marketplaceCreditsGoalsOfTheWeekController.OnOpenSection();
                    break;
                case MarketplaceCreditsSection.WEEK_GOALS_COMPLETED:
                    view.WeekGoalsCompletedView.gameObject.SetActive(true);
                    break;
                case MarketplaceCreditsSection.PROGRAM_ENDED:
                    view.ProgramEndedView.gameObject.SetActive(true);
                    break;
            }

            view.TotalCreditsWidget.gameObject.SetActive(section != MarketplaceCreditsSection.WELCOME);
        }

        public void ShowCreditsUnlockedPanel()
        {
            showCreditsUnlockedCts = showCreditsUnlockedCts.SafeRestart();
            mvcManager.ShowAsync(CreditsUnlockedController.IssueCommand(), showCreditsUnlockedCts.Token).Forget();
        }

        public void ShowErrorNotification(string message)
        {
            showErrorNotificationCts = showErrorNotificationCts.SafeRestart();
            ShowErrorNotificationAsync(message, showErrorNotificationCts.Token).Forget();
        }

        public void Dispose()
        {
            showHideMenuCts.SafeCancelAndDispose();
            showCreditsUnlockedCts.SafeCancelAndDispose();
            showErrorNotificationCts.SafeCancelAndDispose();

            mvcManager.OnViewClosed -= OnCreditsUnlockedPanelClosed;
            view.InfoLinkButton.onClick.RemoveListener(OpenInfoLink);
            view.TotalCreditsWidget.GoShoppingButton.onClick.RemoveListener(OpenLearnMoreLink);

            foreach (Button closeButton in view.CloseButtons)
                closeButton.onClick.RemoveListener(ClosePanel);

            marketplaceCreditsWelcomeController.Dispose();
            marketplaceCreditsGoalsOfTheWeekController.Dispose();
        }

        private void CloseAllSections()
        {
            view.WelcomeView.gameObject.SetActive(false);
            view.GoalsOfTheWeekView.gameObject.SetActive(false);
            view.WeekGoalsCompletedView.gameObject.SetActive(false);
            view.ProgramEndedView.gameObject.SetActive(false);
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

        private void OnMarketplaceCreditsNotificationClicked(object[] parameters) =>
            OpenPanel(forceReopen: true);

        private async UniTaskVoid TestNotificationAsync(CancellationToken ct)
        {
            await UniTask.Delay(5000, cancellationToken: ct);
            notificationBusController.AddNotification(new MarketplaceCreditsNotification
            {
                Type = NotificationType.MARKETPLACE_CREDITS,
                Address = "0x1b8BA74cC34C2927aac0a8AF9C3B1BA2e61352F2",
                Id = $"SantiTest{DateTime.Now.Ticks}",
                Read = false,
                Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString(),
                Metadata = new MarketplaceCreditsNotificationMetadata
                {
                    Title = "Weekly Goal Completed!",
                    Description = "Claim your Credits to unlock them",
                    Image = "https://i.ibb.co/4L0WD2j/Credits-Icn.png",
                }
            });
        }
    }
}
