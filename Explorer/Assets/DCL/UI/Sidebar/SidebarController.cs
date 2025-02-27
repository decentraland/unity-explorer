using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Chat.History;
using DCL.ExplorePanel;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.SidebarBus;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.SharedSpaceManager;
using DCL.UI.Skybox;
using DCL.Web3.Identities;
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
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWebBrowser webBrowser;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly ChatView chatView;
        private readonly IChatHistory chatHistory;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource systemMenuCts = new ();

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
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
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
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
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
                OpenExplorePanelInSection(ExploreSections.Backpack);
            });

            viewInstance.settingsButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Settings));
            viewInstance.mapButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Navmap));

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfileMenu);
            viewInstance.sidebarSettingsButton.onClick.AddListener(OpenSidebarSettings);
            viewInstance.notificationsButton.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.backpackNotificationIndicator.SetActive(false);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
//            viewInstance.sidebarSettingsWidget.OnViewHidden += OnSidebarSettingsClosed;
            viewInstance.skyboxButton.Button.onClick.AddListener(OpenSkyboxSettings);
            viewInstance.SkyboxMenuView.OnViewHidden += OnSkyboxSettingsClosed;
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);
            viewInstance.emotesWheelButton.onClick.AddListener(OnEmotesWheelButtonClicked);

            if (includeCameraReel)
                viewInstance.cameraReelButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.CameraReel));
            else
            {
                viewInstance.cameraReelButton.gameObject.SetActive(false);
                viewInstance.InWorldCameraButton.gameObject.SetActive(false);
            }

            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);

            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;
            chatView.FoldingChanged += OnChatViewFoldingChanged;

            sharedSpaceManager.RegisterPanelController(PanelsSharingSpace.Notifications, notificationsMenuController);
            sharedSpaceManager.RegisterPanelController(PanelsSharingSpace.Skybox, skyboxMenuController);
            sharedSpaceManager.RegisterPanelController(PanelsSharingSpace.SidebarProfile, profileMenuController);
            sharedSpaceManager.RegisterPanelController(PanelsSharingSpace.SidebarSettings, viewInstance!.sidebarSettingsWidget);
        }

        private void OnEmotesWheelButtonClicked()
        {
            sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel);
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
            sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Chat);
        }

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();
        }

        private void OnControlsButtonClicked()
        {
            mvcManager.ShowAsync(ControlsPanelController.IssueCommand()).Forget();
 //           sidebarActionsBus.OpenWidget();
        }

        private void OnAutoHideToggleChanged(bool value)
        {
            sidebarBus.SetAutoHideSidebarStatus(value);
        }

        private void CloseAllWidgets()
        {
            systemMenuCts = systemMenuCts.SafeRestart();
/*
            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(systemMenuCts.Token).Forget();

            if (skyboxMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                skyboxMenuController.HideViewAsync(systemMenuCts.Token).Forget();

            notificationsMenuController.ToggleNotificationsPanel(true);
            viewInstance!.sidebarSettingsWidget.CloseElement();*/
            sidebarBus.UnblockSidebar();
        }

        private void OpenSidebarSettings()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();

            sharedSpaceManager.ShowAsync(PanelsSharingSpace.SidebarSettings).Forget();
            viewInstance.sidebarSettingsButton.OnSelect(null);
            viewInstance.sidebarSettingsWidget.Closed += OnSidebarSettingsClosed;

 //           sidebarActionsBus.OpenWidget();
        }

        private void OnSidebarSettingsClosed()
        {
            viewInstance.sidebarSettingsWidget.Closed -= OnSidebarSettingsClosed;
            sharedSpaceManager.HideAsync(PanelsSharingSpace.SidebarSettings).Forget();
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
            UpdateFrameColorAsync().Forget();
        }

        private async UniTaskVoid UpdateFrameColorAsync()
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, profileWidgetCts.Token);

            if (profile != null)
                viewInstance!.FaceFrame.color = profile.UserNameColor;
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
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.SidebarProfile).Forget();
//            profileMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), systemMenuCts.Token).Forget();
 //           sidebarActionsBus.OpenWidget();
        }

        private void OpenSkyboxSettings()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();

            systemMenuCts = systemMenuCts.SafeRestart();
            viewInstance!.skyboxButton.SetSelected(true);
            sharedSpaceManager.ShowAsync(PanelsSharingSpace.Skybox).Forget();
//            skyboxMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), systemMenuCts.Token).Forget();
//            sidebarActionsBus.OpenWidget();
        }

        private void OnSkyboxSettingsClosed()
        {
            sidebarBus.UnblockSidebar();
            viewInstance!.skyboxButton.SetSelected(false);
        }

        private async void OpenNotificationsPanel()
        {
            CloseAllWidgets();
            sidebarBus.BlockSidebar();
            await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Notifications);
//            notificationsMenuController.ToggleNotificationsPanel(false);
 //           sidebarActionsBus.OpenWidget();
        }

        private async void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            CloseAllWidgets();

            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
//            sidebarActionsBus.OpenWidget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
