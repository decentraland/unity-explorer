using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Chat.History;
using DCL.ExplorePanel;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.SidebarBus;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.SharedSpaceManager;
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
        private readonly ProfileMenuController profileMenuController;
        private readonly SkyboxMenuController skyboxMenuController;
        private readonly ControlsPanelController controlsPanelController;
        private readonly IWebBrowser webBrowser;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly ChatView chatView;
        private readonly IChatHistory chatHistory;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource profileWidgetCts = new ();

        public event Action? HelpOpened;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public SidebarController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            INotificationsBusController notificationsBusController,
            NotificationsMenuController notificationsMenuController,
            ProfileWidgetController profileIconWidgetController,
            ProfileMenuController profileMenuMenuWidgetController,
            SkyboxMenuController skyboxMenuController,
            ControlsPanelController controlsPanelController,
            ISidebarBus sidebarBus,
            IWebBrowser webBrowser,
            bool includeCameraReel,
            bool includeFriends,
            ChatView chatView,
            IChatHistory chatHistory,
            ISharedSpaceManager sharedSpaceManager)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileIconWidgetController = profileIconWidgetController;
            this.profileMenuController = profileMenuMenuWidgetController;
            this.sidebarBus = sidebarBus;
            this.notificationsBusController = notificationsBusController;
            this.notificationsMenuController = notificationsMenuController;
            this.skyboxMenuController = skyboxMenuController;
            this.controlsPanelController = controlsPanelController;
            this.webBrowser = webBrowser;
            this.includeCameraReel = includeCameraReel;
            this.chatView = chatView;
            this.chatHistory = chatHistory;
            this.includeFriends = includeFriends;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        public override void Dispose()
        {
            base.Dispose();

            notificationsMenuController.Dispose(); // TODO: Does it make sense to call this here?
        }

        protected override void OnViewInstantiated()
        {
            mvcManager.RegisterController(controlsPanelController);

            viewInstance!.backpackButton.onClick.AddListener(() =>
            {
                viewInstance.backpackNotificationIndicator.SetActive(false);
                OpenExplorePanelInSectionAsync(ExploreSections.Backpack);
            });

            viewInstance.settingsButton.onClick.AddListener(() => OpenExplorePanelInSectionAsync(ExploreSections.Settings));
            viewInstance.mapButton.onClick.AddListener(() => OpenExplorePanelInSectionAsync(ExploreSections.Navmap));

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfileMenuAsync);
            viewInstance.sidebarSettingsButton.onClick.AddListener(OpenSidebarSettingsAsync);
            viewInstance.notificationsButton.onClick.AddListener(OpenNotificationsPanelAsync);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.backpackNotificationIndicator.SetActive(false);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            viewInstance.skyboxButton.Button.onClick.AddListener(OpenSkyboxSettingsAsync);
            viewInstance.sidebarSettingsWidget.ViewShowingComplete += (panel) => viewInstance.sidebarSettingsButton.OnSelect(null);;
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClickedAsync);
            viewInstance.emotesWheelButton.onClick.AddListener(OnEmotesWheelButtonClickedAsync);

            if (includeCameraReel)
                viewInstance.cameraReelButton.onClick.AddListener(() => OpenExplorePanelInSectionAsync(ExploreSections.CameraReel));
            else
            {
                viewInstance.cameraReelButton.gameObject.SetActive(false);
                viewInstance.InWorldCameraButton.gameObject.SetActive(false);
            }

            if(includeFriends)
                viewInstance.friendsButton.onClick.AddListener(OnFriendsButtonClickedAsync);

            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);

            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;
            chatView.FoldingChanged += OnChatViewFoldingChanged;

            mvcManager.RegisterController(skyboxMenuController);
            mvcManager.RegisterController(profileMenuController);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Notifications, notificationsMenuController);
            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Skybox, skyboxMenuController);
            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.SidebarProfile, profileMenuController);
            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.SidebarSettings, viewInstance!.sidebarSettingsWidget);
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

        private void OnAutoHideToggleChanged(bool value)
        {
            sidebarBus.SetAutoHideSidebarStatus(value);
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
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        #region Sidebar button handlers

        private void OnUnreadMessagesButtonClickedAsync()
        {
            sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Chat, new ChatController.ShowParams(true)).Forget();
        }

        private async void OnEmotesWheelButtonClickedAsync()
        {
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel);
        }

        private async void OnFriendsButtonClickedAsync()
        {
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Friends);
        }

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();
        }

        private void OnControlsButtonClicked()
        {
            mvcManager.ShowAsync(ControlsPanelController.IssueCommand()).Forget();
        }

        private async void OpenSidebarSettingsAsync()
        {
            sidebarBus.BlockSidebar();
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.SidebarSettings);
            sidebarBus.UnblockSidebar();

            viewInstance!.sidebarSettingsButton.OnDeselect(null);
        }

        private async void OpenProfileMenuAsync()
        {
            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)

                //Profile is already open
                return;

            viewInstance!.ProfileMenuView.gameObject.SetActive(true);

            sidebarBus.BlockSidebar();
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.SidebarProfile);
            sidebarBus.UnblockSidebar();
        }

        private async void OpenSkyboxSettingsAsync()
        {
            sidebarBus.BlockSidebar();
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Skybox);
            sidebarBus.UnblockSidebar();
        }

        private async void OpenNotificationsPanelAsync()
        {
            sidebarBus.BlockSidebar();
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Notifications);
            sidebarBus.UnblockSidebar();
        }

        private async void OpenExplorePanelInSectionAsync(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(section, backpackSection));
        }

        #endregion
    }
}
