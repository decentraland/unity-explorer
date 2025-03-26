using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Chat.History;
using DCL.ExplorePanel;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCreditsAPIService;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.SidebarBus;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Sidebar.SidebarActionsBus;
using DCL.UI.Skybox;
using MVC;
using System;
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
        private readonly MarketplaceCreditsMenuController marketplaceCreditsMenuController;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ProfileMenuController profileMenuController;
        private readonly SkyboxMenuController skyboxMenuController;
        private readonly ControlsPanelController controlsPanelController;
        private readonly IWebBrowser webBrowser;
        private readonly ISidebarActionsBus sidebarActionsBus;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly bool includeMarketplaceCredits;
        private readonly ChatView chatView;
        private readonly IChatHistory chatHistory;

        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource systemMenuCts = new ();

        public event Action? HelpOpened;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public SidebarController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            INotificationsBusController notificationsBusController,
            NotificationsMenuController notificationsMenuController,
            MarketplaceCreditsMenuController marketplaceCreditsMenuController,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ProfileWidgetController profileIconWidgetController,
            ProfileMenuController profileMenuMenuWidgetController,
            SkyboxMenuController skyboxMenuController,
            ControlsPanelController controlsPanelController,
            ISidebarBus sidebarBus,
            IWebBrowser webBrowser,
            ISidebarActionsBus sidebarActionsBus,
            bool includeCameraReel,
            bool includeFriends,
            bool includeMarketplaceCredits,
            ChatView chatView,
            IChatHistory chatHistory)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileIconWidgetController = profileIconWidgetController;
            profileMenuController = profileMenuMenuWidgetController;
            this.sidebarBus = sidebarBus;
            this.notificationsBusController = notificationsBusController;
            this.notificationsMenuController = notificationsMenuController;
            this.marketplaceCreditsMenuController = marketplaceCreditsMenuController;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.skyboxMenuController = skyboxMenuController;
            this.controlsPanelController = controlsPanelController;
            this.webBrowser = webBrowser;
            this.sidebarActionsBus = sidebarActionsBus;
            this.includeCameraReel = includeCameraReel;
            this.chatView = chatView;
            this.chatHistory = chatHistory;
            this.includeFriends = includeFriends;
            this.includeMarketplaceCredits = includeMarketplaceCredits;

            sidebarActionsBus.SubscribeOnCloseAllWidgets(CloseAllWidgets);
        }

        public override void Dispose()
        {
            base.Dispose();

            notificationsMenuController.Dispose();
            marketplaceCreditsMenuController.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            mvcManager.RegisterController(controlsPanelController);

            viewInstance!.backpackButton.onClick.AddListener(() =>
            {
                viewInstance.backpackNotificationIndicator.SetActive(false);
                OpenExplorePanelInSection(ExploreSections.Backpack);
            });

            viewInstance.settingsButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Settings));
            viewInstance.mapButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Navmap));

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfileMenu);
            viewInstance.sidebarSettingsButton.onClick.AddListener(OpenSidebarSettings);
            viewInstance.notificationsButton.Button.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.MarketplaceCreditsButton.Button.onClick.AddListener(OpenMarketplaceCreditsPanel);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.backpackNotificationIndicator.SetActive(false);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            viewInstance.sidebarSettingsWidget.OnViewHidden += OnSidebarSettingsClosed;
            viewInstance.skyboxButton.Button.onClick.AddListener(OpenSkyboxSettings);
            viewInstance.SkyboxMenuView.OnViewHidden += OnSkyboxSettingsClosed;
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);

            if (includeCameraReel)
                viewInstance.cameraReelButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.CameraReel));
            else
            {
                viewInstance.cameraReelButton.gameObject.SetActive(false);
                viewInstance.InWorldCameraButton.gameObject.SetActive(false);
            }

            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);
            viewInstance.MarketplaceCreditsButton.gameObject.SetActive(includeMarketplaceCredits);

            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;
            chatView.FoldingChanged += OnChatViewFoldingChanged;

            marketplaceCreditsMenuController.CheckForSidebarButtonState();
        }

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;
        }

        private void OnChatViewFoldingChanged(bool isUnfolded)
        {
            // TODO: The sidebar should provide a mechanism to fix the icon of a button, so it can be active while the Chat window is unfolded
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;
        }

        private void OnUnreadMessagesButtonClicked()
        {
            if (!chatView.IsUnfolded)
            {
                chatView.IsUnfolded = true;
                chatView.ShowNewMessages();
            }
            else
            {
                chatView.IsUnfolded = false;
            }
        }

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();

            // TODO (Santi): Remove this!!
            TestMarketplaceCreditsNotificationAsync(CancellationToken.None).Forget();
        }

        private void OnControlsButtonClicked()
        {
            mvcManager.ShowAsync(ControlsPanelController.IssueCommand()).Forget();
            sidebarActionsBus.OpenWidget();

            // TODO (Santi): Remove this!!
            marketplaceCreditsAPIClient.SubscribeEmailAsync(string.Empty, CancellationToken.None).Forget();
        }

        private void OnAutoHideToggleChanged(bool value)
        {
            sidebarBus.SetAutoHideSidebarStatus(value);
        }

        private void CloseAllWidgets()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(systemMenuCts.Token).Forget();

            if (skyboxMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                skyboxMenuController.HideViewAsync(systemMenuCts.Token).Forget();

            notificationsMenuController.ToggleNotificationsPanel(true);
            marketplaceCreditsMenuController.ClosePanel();
            viewInstance!.sidebarSettingsWidget.CloseElement();
            sidebarBus.UnblockSidebar();
        }

        private void OpenSidebarSettings()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            viewInstance!.sidebarSettingsWidget.ShowAsync(CancellationToken.None).Forget();
            viewInstance.sidebarSettingsButton.OnSelect(null);
            sidebarActionsBus.OpenWidget();
        }

        private void OnSidebarSettingsClosed()
        {
            sidebarBus.UnblockSidebar();
            viewInstance!.sidebarSettingsButton.OnDeselect(null);
        }

        private void OnRewardNotificationClicked(object[] parameters)
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
        }

        private void OnRewardNotificationReceived(INotification newNotification)
        {
            viewInstance!.backpackNotificationIndicator.SetActive(true);
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

        private void OpenProfileMenu()
        {
            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)

                //Profile is already open
                return;

            CloseAllWidgets();
            sidebarBus.BlockSidebar();

            systemMenuCts = systemMenuCts.SafeRestart();
            viewInstance!.ProfileMenuView.gameObject.SetActive(true);
            profileMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), systemMenuCts.Token).Forget();
            sidebarActionsBus.OpenWidget();
        }

        private void OpenSkyboxSettings()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();

            systemMenuCts = systemMenuCts.SafeRestart();
            viewInstance!.skyboxButton.SetSelected(true);
            skyboxMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), systemMenuCts.Token).Forget();
            sidebarActionsBus.OpenWidget();
        }

        private void OnSkyboxSettingsClosed()
        {
            sidebarBus.UnblockSidebar();
            viewInstance!.skyboxButton.SetSelected(false);
        }

        private void OpenNotificationsPanel()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            notificationsMenuController.ToggleNotificationsPanel(false);
            sidebarActionsBus.OpenWidget();
        }

        private void OpenMarketplaceCreditsPanel()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            marketplaceCreditsMenuController.OpenPanel();
            sidebarActionsBus.OpenWidget();
        }

        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            CloseAllWidgets();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
            sidebarActionsBus.OpenWidget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private async UniTaskVoid TestMarketplaceCreditsNotificationAsync(CancellationToken ct)
        {
            await UniTask.Delay(5000, cancellationToken: ct);
            notificationsBusController.AddNotification(new MarketplaceCreditsNotification
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
