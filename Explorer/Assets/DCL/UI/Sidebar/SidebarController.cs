using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using DCL.Notification;
using DCL.Notification.NotificationsBus;
using DCL.Notification.NotificationsMenu;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        private readonly IMVCManager mvcManager;
        private readonly ProfileWidgetController profileIconWidgetController;
        private readonly ProfileWidgetController profileMenuWidgetController;
        private readonly SystemMenuController systemMenuController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationsMenuController notificationsMenuController;

        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource systemMenuCts = new ();

        public SidebarController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            INotificationsBusController notificationsBusController,
            NotificationsMenuController notificationsMenuController,
            ProfileWidgetController profileIconWidgetController,
            ProfileWidgetController profileMenuWidgetController,
            SystemMenuController systemMenuController)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileIconWidgetController = profileIconWidgetController;
            this.profileMenuWidgetController = profileMenuWidgetController;
            this.systemMenuController = systemMenuController;
            this.notificationsBusController = notificationsBusController;
            this.notificationsMenuController = notificationsMenuController;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.backpackButton.onClick.AddListener(() =>
            {
                viewInstance.backpackNotificationIndicator.SetActive(false);
                OpenExplorePanelInSection(ExploreSections.Backpack);
            });
            viewInstance.settingsButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Settings));
            viewInstance.mapButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Navmap));
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfilePopup);
            viewInstance.notificationsButton.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.backpackNotificationIndicator.SetActive(false);
            notificationsBusController.SubscribeToNotificationType(NotificationType.REWARD_ASSIGNMENT,  OnRewardNotificationReceived);
        }

        private void OnRewardNotificationReceived(object[] parameters)
        {
            viewInstance.backpackNotificationIndicator.SetActive(true);
        }

        protected override void OnViewShow()
        {
            profileWidgetCts = profileWidgetCts.SafeRestart();
            profileIconWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token).Forget();

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(CancellationToken.None).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            profileWidgetCts.SafeCancelAndDispose();
            systemMenuCts.SafeCancelAndDispose();
        }

        private void OpenProfilePopup()
        {
            viewInstance.profileMenu.gameObject.SetActive(true);
            ShowSystemMenu();
        }

        private void OpenNotificationsPanel()
        {
            notificationsMenuController.ToggleNotificationsPanel();
        }

        private void ShowSystemMenu()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            async UniTaskVoid ShowSystemMenuAsync(CancellationToken ct)
            {
                await systemMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0),
                    new ControllerNoData(), ct);
                await systemMenuController.HideViewAsync(ct);
            }

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(systemMenuCts.Token).Forget();
            else
            {
                profileMenuWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token).Forget();
                ShowSystemMenuAsync(systemMenuCts.Token).Forget();
            }
        }


        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

    }
}
