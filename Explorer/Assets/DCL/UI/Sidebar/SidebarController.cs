using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.MarketplaceCredits;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.Profiles.Self;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.SharedSpaceManager;
using DCL.UI.Skybox;
using ECS;
using MVC;
using System;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.Chat.ChatStates;
using DCL.ChatArea;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using Runtime.Wearables;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private readonly IMVCManager mvcManager;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly NotificationsPanelController notificationsPanelController;
        private readonly ProfileMenuController profileMenuController;
        private readonly SkyboxMenuController skyboxMenuController;
        private readonly ControlsPanelController controlsPanelController;
        private readonly SmartWearablesSideBarTooltipController smartWearablesTooltipController;
        private readonly IWebBrowser webBrowser;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly ChatMainSharedAreaView chatMainView;
        private readonly IChatHistory chatHistory;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly URLBuilder urlBuilder = new ();
        private readonly SmartWearableCache smartWearablesCache;

        private bool includeMarketplaceCredits;
        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource checkForMarketplaceCreditsFeatureCts = new ();
        private CancellationTokenSource? referralNotificationCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? HelpOpened;

        public SidebarController(
            ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            NotificationsPanelController notificationsPanelController,
            SidebarProfileButtonPresenter profileButtonPresenter,
            ProfileMenuController profileMenuMenuWidgetController,
            SkyboxMenuController skyboxMenuController,
            ControlsPanelController controlsPanelController,
            SmartWearablesSideBarTooltipController smartWearablesTooltipController,
            IWebBrowser webBrowser,
            bool includeCameraReel,
            bool includeFriends,
            bool includeMarketplaceCredits,
            ChatMainSharedAreaView chatMainView,
            IChatHistory chatHistory,
            ISharedSpaceManager sharedSpaceManager,
            ISelfProfile selfProfile,
            IRealmData realmData,
            IDecentralandUrlsSource decentralandUrlsSource,
            IEventBus eventBus,
            SmartWearableCache smartWearableCache)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileButtonPresenter = profileButtonPresenter;
            profileMenuController = profileMenuMenuWidgetController;
            this.notificationsPanelController = notificationsPanelController;
            this.skyboxMenuController = skyboxMenuController;
            this.controlsPanelController = controlsPanelController;
            this.smartWearablesTooltipController = smartWearablesTooltipController;
            this.webBrowser = webBrowser;
            this.includeCameraReel = includeCameraReel;
            this.chatMainView = chatMainView;
            this.chatHistory = chatHistory;
            this.includeFriends = includeFriends;
            this.includeMarketplaceCredits = includeMarketplaceCredits;
            this.sharedSpaceManager = sharedSpaceManager;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            smartWearablesCache = smartWearableCache;

            eventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            notificationsPanelController.Dispose(); // TODO: Does it make sense to call this here?
            checkForMarketplaceCreditsFeatureCts.SafeCancelAndDispose();
            referralNotificationCts.SafeCancelAndDispose();
            checkForCommunitiesFeatureCts.SafeCancelAndDispose();
        }

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent eventData) =>
            OnChatViewFoldingChanged(eventData.CurrentState is not HiddenChatState && eventData.CurrentState is not MinimizedChatState);

        protected override void OnViewInstantiated()
        {
            mvcManager.OnViewShowed += OnMvcManagerViewShowed;
            mvcManager.OnViewClosed += OnMvcManagerViewClosed;

            viewInstance!.backpackNotificationIndicator.SetActive(false);
            viewInstance.skyboxButton.interactable = true;
            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);
            viewInstance.cameraReelButton.gameObject.SetActive(includeCameraReel);
            viewInstance.InWorldCameraButton.gameObject.SetActive(includeCameraReel);

            SubscribeToEvents();

            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;

            //chatView.FoldingChanged += OnChatViewFoldingChanged;
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Notifications, notificationsMenuController);
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Skybox, skyboxMenuController);
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Controls, controlsPanelController);
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.SmartWearables, smartWearablesTooltipController);
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.SidebarProfile, profileMenuController);
            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.SidebarSettings, viewInstance!.sidebarSettingsWidget);

            checkForMarketplaceCreditsFeatureCts = checkForMarketplaceCreditsFeatureCts.SafeRestart();
            CheckForMarketplaceCreditsFeatureAsync(checkForMarketplaceCreditsFeatureCts.Token).Forget();

            checkForCommunitiesFeatureCts = checkForCommunitiesFeatureCts.SafeRestart();
            CheckForCommunitiesFeatureAsync(checkForCommunitiesFeatureCts.Token).Forget();

            OnChatViewFoldingChanged(true);
        }

        private void SubscribeToEvents()
        {
            viewInstance!.settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            viewInstance.communitiesButton.onClick.AddListener(OnCommunitiesButtonClicked);
            viewInstance.mapButton.onClick.AddListener(OnMapButtonClicked);
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfileMenuAsync);
            viewInstance.sidebarSettingsButton.onClick.AddListener(OpenSidebarSettingsAsync);
            viewInstance.notificationsButton.onClick.AddListener(OpenNotificationsPanelAsync);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            viewInstance.skyboxButton.onClick.AddListener(OpenSkyboxSettingsAsync);
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);
            viewInstance.emotesWheelButton.onClick.AddListener(OnEmotesWheelButtonClickedAsync);
            viewInstance.backpackButton.onClick.AddListener(OnBackpackButtonClicked);
            viewInstance.SmartWearablesButton.OnButtonHover += OnSmartWearablesButtonHover;
            viewInstance.SmartWearablesButton.OnButtonUnhover += OnSmartWearablesButtonUnhover;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            if (includeCameraReel) viewInstance.cameraReelButton.onClick.AddListener(OnCameraReelButtonClicked);
            if (includeFriends) viewInstance.friendsButton.onClick.AddListener(OnFriendsButtonClickedAsync);
        }

        private void OnSettingsButtonClicked()
        {
            OpenExplorePanelInSectionAsync(ExploreSections.Settings).Forget();
        }

        private void OnCommunitiesButtonClicked()
        {
            OpenExplorePanelInSectionAsync(ExploreSections.Communities).Forget();
        }

        private void OnMapButtonClicked()
        {
            OpenExplorePanelInSectionAsync(ExploreSections.Navmap).Forget();
        }

        private void OnBackpackButtonClicked()
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
            OpenExplorePanelInSectionAsync(ExploreSections.Backpack).Forget();
        }

        private void OnCameraReelButtonClicked()
        {
            OpenExplorePanelInSectionAsync(ExploreSections.CameraReel).Forget();
        }

        private void OnReferralNewTierNotificationClicked(object[] parameters)
        {
            referralNotificationCts = referralNotificationCts.SafeRestart();
            OpenReferralWebsiteAsync(referralNotificationCts.Token).Forget();
            return;

            async UniTaskVoid OpenReferralWebsiteAsync(CancellationToken ct)
            {
                try
                {
                    Profile? myProfile = await selfProfile.ProfileAsync(ct);
                    if (myProfile == null) return;

                    urlBuilder.Clear();

                    URLAddress url = urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Host)))
                                               .AppendPath(URLPath.FromString($"profile/accounts/{myProfile.UserId}/referral"))
                                               .Build();

                    webBrowser.OpenUrl(url);
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
            }
        }

        private void OnMvcManagerViewClosed(IController closedController)
        {
            // Panels that are controllers and can be opened using shortcuts
            if (closedController is EmotesWheelController)
                viewInstance?.emotesWheelButton.animator.SetTrigger(UIAnimationHashes.EMPTY);
            else if (closedController is FriendsPanelController)
                viewInstance?.friendsButton.animator.SetTrigger(UIAnimationHashes.EMPTY);
        }

        private void OnMvcManagerViewShowed(IController showedController)
        {
            // Panels that are controllers and can be opened using shortcuts
            if (showedController is EmotesWheelController)
                viewInstance?.emotesWheelButton.animator.SetTrigger(UIAnimationHashes.ACTIVE);
            else if (showedController is FriendsPanelController)
                viewInstance?.friendsButton.animator.SetTrigger(UIAnimationHashes.ACTIVE);
        }

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)
        {
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;
        }

        private void OnChatViewFoldingChanged(bool isUnfolded)
        {
            viewInstance?.unreadMessagesButton.animator.ResetTrigger(!isUnfolded ? UIAnimationHashes.ACTIVE : UIAnimationHashes.EMPTY);
            viewInstance?.unreadMessagesButton.animator.SetTrigger(isUnfolded ? UIAnimationHashes.ACTIVE : UIAnimationHashes.EMPTY);
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;
        }

        private void OnAutoHideToggleChanged(bool value)
        {
            viewInstance?.SetAutoHideSidebarStatus(value);
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
            profileButtonPresenter.LoadProfile();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            profileWidgetCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private async UniTaskVoid CheckForMarketplaceCreditsFeatureAsync(CancellationToken ct)
        {
            viewInstance?.marketplaceCreditsButton.gameObject.SetActive(false);

            await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
            Profile? ownProfile = await selfProfile.ProfileAsync(ct);

            if (ownProfile == null)
                return;

            includeMarketplaceCredits = MarketplaceCreditsUtils.IsUserAllowedToUseTheFeatureAsync(
                includeMarketplaceCredits,
                ownProfile.UserId,
                ct);

            viewInstance?.marketplaceCreditsButton.gameObject.SetActive(includeMarketplaceCredits);

            if (includeMarketplaceCredits)
                viewInstance?.marketplaceCreditsButton.Button.onClick.AddListener(OnMarketplaceCreditsButtonClickedAsync);
        }

        private async UniTaskVoid CheckForCommunitiesFeatureAsync(CancellationToken ct)
        {
            viewInstance?.communitiesButton.gameObject.SetActive(false);
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);
            viewInstance?.communitiesButton.gameObject.SetActive(includeCommunities);
        }

#region Sidebar button handlers
        private void OnUnreadMessagesButtonClicked()
        {
            // Note: It is persistent, it's not possible to wait for it to close, it is managed with events
            sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true)).Forget();
        }

        private async void OnEmotesWheelButtonClickedAsync()
        {
            await mvcManager.ShowAsync(EmotesWheelController.IssueCommand());

            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel);
        }

        private async void OnFriendsButtonClickedAsync()
        {
            await mvcManager.ShowAsync(FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS)));

            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS));
        }

        private async void OnMarketplaceCreditsButtonClickedAsync()
        {
            await mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)));

            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.MarketplaceCredits, new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false));
        }

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();
        }

        private void OnControlsButtonClicked()
        {
            mvcManager.ShowAndForget(ControlsPanelController.IssueCommand());
        }

        private async void OpenSidebarSettingsAsync()
        {
            if (viewInstance == null) return;

            viewInstance.BlockSidebar();

            await mvcManager.ShowAsync(SidebarSettingsWidgetController.IssueCommand());
            viewInstance.UnblockSidebar();

            viewInstance.sidebarSettingsButton.OnDeselect(null);
        }

        private async void OpenProfileMenuAsync()
        {
            viewInstance!.BlockSidebar();
            await mvcManager.ShowAsync(ProfileMenuController.IssueCommand());
            viewInstance.UnblockSidebar();

            //viewInstance!.ProfileMenuView.gameObject.SetActive(true);
            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.SidebarProfile);
        }

        private async void OpenSkyboxSettingsAsync()
        {
            if (viewInstance == null) return;

            viewInstance.BlockSidebar();
            viewInstance.skyboxButton.animator.SetTrigger(UIAnimationHashes.ACTIVE);

            if (skyboxMenuController.State is not (ControllerState.ViewFocused or ControllerState.ViewBlurred or ControllerState.ViewShowing))
                await mvcManager.ShowAsync(SkyboxMenuController.IssueCommand());

            //TODO FRAN: Add proper cancellation  + handle errors on async calls
            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Skybox);

            viewInstance.skyboxButton.animator.SetTrigger(UIAnimationHashes.EMPTY);
            viewInstance.UnblockSidebar();
        }

        private async void OpenNotificationsPanelAsync()
        {
            if (viewInstance == null) return;

            viewInstance.BlockSidebar();
            viewInstance.notificationsButton.animator.SetTrigger(UIAnimationHashes.ACTIVE);

            await mvcManager.ShowAsync(NotificationsPanelController.IssueCommand());

            //await sharedSpaceManager.ToggleVisibilityAsync(PanelsSharingSpace.Notifications);

            viewInstance.notificationsButton.animator.SetTrigger(UIAnimationHashes.EMPTY);
            viewInstance.UnblockSidebar();
        }

        private async UniTaskVoid OpenExplorePanelInSectionAsync(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            // Note: The buttons of these options (map, backpack, etc.) are not highlighted because they are not visible anyway
            await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section, backpackSection)));

            //await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(section, backpackSection), PanelsSharingSpace.Chat);
        }

        private void OnSmartWearablesButtonHover()
        {
            mvcManager.ShowAndForget(SmartWearablesSideBarTooltipController.IssueCommand());

            //sharedSpaceManager.ShowAsync(PanelsSharingSpace.SmartWearables).Forget();
        }

        private void OnSmartWearablesButtonUnhover()
        {
            smartWearablesTooltipController.Close();
        }
#endregion
    }
}
