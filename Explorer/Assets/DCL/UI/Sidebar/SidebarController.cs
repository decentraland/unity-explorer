using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.SidebarBus;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private readonly IMVCManager mvcManager;
        private readonly ProfileWidgetController profileIconWidgetController;
        private readonly ISidebarBus sidebarBus;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationsMenuController notificationsMenuController;
        private readonly SidebarProfileController sidebarProfileController;

        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource systemMenuCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public SidebarController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            INotificationsBusController notificationsBusController,
            NotificationsMenuController notificationsMenuController,
            ProfileWidgetController profileIconWidgetController,
            SidebarProfileController profileMenuWidgetController,
            ISidebarBus sidebarBus)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileIconWidgetController = profileIconWidgetController;
            this.sidebarProfileController = profileMenuWidgetController;
            this.sidebarBus = sidebarBus;
            this.notificationsBusController = notificationsBusController;
            this.notificationsMenuController = notificationsMenuController;
        }

        public override void Dispose()
        {
            base.Dispose();

            notificationsMenuController.Dispose();
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
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfileWidget);
            viewInstance.sidebarSettingsButton.onClick.AddListener(OpenSidebarSettings);
            viewInstance.notificationsButton.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.backpackNotificationIndicator.SetActive(false);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            viewInstance.sidebarSettingsWidget.OnViewHidden += OnSidebarSettingsClosed;
        }

        private void OnAutoHideToggleChanged(bool value)
        {
            sidebarBus.SetAutoHideSidebarStatus(value);
        }

        private void CloseAllWidgets()
        {
            systemMenuCts = systemMenuCts.SafeRestart();
            if (sidebarProfileController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred) { sidebarProfileController.HideViewAsync(systemMenuCts.Token).Forget(); }
            notificationsMenuController.ToggleNotificationsPanel(true);
            viewInstance.sidebarSettingsWidget.CloseElement();
            sidebarBus.UnblockSidebar();
        }

        private void OpenSidebarSettings()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            viewInstance.sidebarSettingsWidget.ShowAsync(CancellationToken.None).Forget();
            viewInstance.sidebarSettingsButton.OnSelect(null);
        }

        private void OnSidebarSettingsClosed()
        {
            sidebarBus.UnblockSidebar();
            viewInstance.sidebarSettingsButton.OnDeselect(null);
        }

        private void OnRewardNotificationClicked(object[] parameters)
        {
            viewInstance.backpackNotificationIndicator.SetActive(false);
        }

        private void OnRewardNotificationReceived(INotification newNotification)
        {
            viewInstance.backpackNotificationIndicator.SetActive(true);
        }

        protected override void OnViewShow()
        {
            profileWidgetCts = profileWidgetCts.SafeRestart();
            //We load the data into the profile widget
            profileIconWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            profileWidgetCts.SafeCancelAndDispose();
            systemMenuCts.SafeCancelAndDispose();
        }

        private void OpenProfileWidget()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            viewInstance.profileMenu.gameObject.SetActive(true);
            ToggleSystemMenu();
        }

        private void OpenNotificationsPanel()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            notificationsMenuController.ToggleNotificationsPanel(false);
        }

        private void ToggleSystemMenu()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            if (sidebarProfileController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
            {
                sidebarProfileController.HideViewAsync(systemMenuCts.Token).Forget();
                sidebarBus.UnblockSidebar();
            }
            else
                sidebarProfileController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), systemMenuCts.Token).Forget();
        }

        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            CloseAllWidgets();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
