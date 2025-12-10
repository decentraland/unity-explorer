using Arch.Core;
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
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera;
using DCL.Chat.ChatStates;
using DCL.ChatArea;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.UI.Buttons;
using ECS.Abstract;
using Runtime.Wearables;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private const string SOURCE_BUTTON = "Button";

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
        private readonly SidebarPanelsShortcutsHandler sidebarPanelsShortcutsHandler;
        private readonly World globalWorld;

        private SingleInstanceEntity? camera => cameraInternal ??= globalWorld.CacheCamera();
        private bool includeMarketplaceCredits;
        private CancellationTokenSource checkForMarketplaceCreditsFeatureCts = new ();
        private CancellationTokenSource? referralNotificationCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();
        private SingleInstanceEntity? cameraInternal;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? HelpOpened;

        public SidebarController(ViewFactoryMethod viewFactory,
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
            SmartWearableCache smartWearableCache,
            EmotesBus emotesBus,
            World globalWorld)
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
            this.globalWorld = globalWorld;
            smartWearablesCache = smartWearableCache;

            sidebarPanelsShortcutsHandler = new SidebarPanelsShortcutsHandler(mvcManager, DCLInput.Instance, emotesBus, globalWorld);

            eventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            sidebarPanelsShortcutsHandler.Dispose();
            // TODO FRAN: Does it make sense to call this here? or should we dispose on the plugin?
            notificationsPanelController.Dispose();
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
            viewInstance.skyboxButton.Button.interactable = true;
            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);
            viewInstance.cameraReelButton.gameObject.SetActive(includeCameraReel);
            viewInstance.InWorldCameraButton.gameObject.SetActive(includeCameraReel);

            SubscribeToEvents();

            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;

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
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);
            viewInstance.InWorldCameraButton.onClick.AddListener(OnOpenCameraButtonClicked);

            viewInstance.emotesWheelButton.Button.onClick.AddListener(OnEmotesWheelButtonClicked);
            viewInstance.NotificationsButton.Button.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.skyboxButton.Button.onClick.AddListener(OpenSkyboxSettingsPanel);
            viewInstance.ProfileWidget.OpenProfileButton.Button.onClick.AddListener(OpenProfilePanel);
            viewInstance.sidebarConfigButton.Button.onClick.AddListener(OpenSidebarSettings);

            viewInstance.backpackButton.onClick.AddListener(OnBackpackButtonClicked);
            viewInstance.smartWearablesButton.OnButtonHover += OnSmartWearablesButtonHover;
            viewInstance.smartWearablesButton.OnButtonUnhover += OnSmartWearablesButtonUnhover;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            if (includeCameraReel) viewInstance.cameraReelButton.onClick.AddListener(OnCameraReelButtonClicked);
            if (includeFriends) viewInstance.friendsButton.Button.onClick.AddListener(OnFriendsButtonClicked);
        }

        private void OnOpenCameraButtonClicked()
        {
            if (globalWorld.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled && !globalWorld.Has<ToggleInWorldCameraRequest>(camera!.Value))
                globalWorld.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = !globalWorld.Has<InWorldCameraComponent>(camera!.Value), Source = SOURCE_BUTTON });
        }

        private void OnSettingsButtonClicked()
        {
            OpenExplorePanelInSection(ExploreSections.Settings);
        }

        private void OnCommunitiesButtonClicked()
        {
            OpenExplorePanelInSection(ExploreSections.Communities);
        }

        private void OnMapButtonClicked()
        {
            OpenExplorePanelInSection(ExploreSections.Navmap);
        }

        private void OnBackpackButtonClicked()
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
            OpenExplorePanelInSection(ExploreSections.Backpack);
        }

        private void OnCameraReelButtonClicked()
        {
            OpenExplorePanelInSection(ExploreSections.CameraReel);
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
            //When panels are closed through shortcuts we need to be able to de-select the buttons
            //Do we care WHICH button was selected?? we can just try to deselect all!?
            switch (closedController)
            {
                case EmotesWheelController:
                    viewInstance?.emotesWheelButton.Deselect(); break;
                case FriendsPanelController:
                    viewInstance?.friendsButton.Deselect(); break;
            }
        }

        private void OnMvcManagerViewShowed(IController showedController)
        {
            // Panels that are controllers and can be opened using shortcuts,
            // should we listen for the shortcuts instead??
            // the problem there is that if we open the panel from anywhere else it wont get selected...
            switch (showedController)
            {
                case EmotesWheelController:
                    viewInstance?.emotesWheelButton.Select();
                    break;
                case FriendsPanelController:
                    viewInstance?.friendsButton.Select();
                    break;
            }
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
            //We load the data into the profile widget
            profileButtonPresenter.LoadProfile();
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
                viewInstance?.marketplaceCreditsButton.Button.onClick.AddListener(OnMarketplaceCreditsButtonClicked);
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

        private void OnEmotesWheelButtonClicked()
        {
            OpenPanelAsync(viewInstance!.emotesWheelButton, mvcManager.ShowAsync(EmotesWheelController.IssueCommand())).Forget();
        }

        private void OnFriendsButtonClicked()
        {
            OpenPanelAsync(viewInstance!.friendsButton, mvcManager.ShowAsync(FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS)))).Forget();
        }

        private void OnMarketplaceCreditsButtonClicked()
        {
            OpenPanelAsync(viewInstance!.sidebarConfigButton,
                mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false)))).Forget();
        }

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();
        }

        private void OnControlsButtonClicked()
        {
            //We don't "select" the controls button as it doesn't have any visual change of state.
            OpenPanelAsync(null, mvcManager.ShowAsync(ControlsPanelController.IssueCommand())).Forget();
        }

        private void OpenSidebarSettings()
        {
            OpenPanelAsync(viewInstance!.sidebarConfigButton, mvcManager.ShowAsync(SidebarSettingsWidgetController.IssueCommand())).Forget();
        }

        private void OpenProfilePanel()
        {
            //We don't "select" the profile button as it doesn't have any visual change of state.
            OpenPanelAsync(null, mvcManager.ShowAsync(ProfileMenuController.IssueCommand())).Forget();
        }

        private void OpenSkyboxSettingsPanel()
        {
            OpenPanelAsync(viewInstance!.skyboxButton, mvcManager.ShowAsync(SkyboxMenuController.IssueCommand())).Forget();
        }

        private async UniTaskVoid OpenPanelAsync(ISelectableButton? button, UniTask showTask)
        {
            //TODO FRAN: DO we need a cancellation token and error handling?
            //Instead of a UniTask maybe we can get the ShowCommand like ShowAsync??
            //We can make all of these into commands so they can be better reused in other parts.

            if (!viewInstance) return;

            viewInstance.BlockSidebar();
            button?.Select();
            await showTask;
            button?.Deselect();
            viewInstance.UnblockSidebar();
        }

        private void OpenNotificationsPanel()
        {
            OpenPanelAsync(viewInstance!.NotificationsButton, mvcManager.ShowAsync(NotificationsPanelController.IssueCommand())).Forget();
        }

        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            // Note: The buttons of these options (map, backpack, etc.) are not selected because they are not visible anyway, same reason we don't lock the sidebar.
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section, backpackSection)));
        }

        private void OnSmartWearablesButtonHover()
        {
            OpenPanelAsync(viewInstance!.smartWearablesButton, mvcManager.ShowAsync(SmartWearablesSideBarTooltipController.IssueCommand())).Forget();
        }

        private void OnSmartWearablesButtonUnhover()
        {
            smartWearablesTooltipController.Close();
        }
#endregion
    }
}
