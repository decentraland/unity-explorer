using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Chat.ChatStates;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.MarketplaceCredits;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Skybox;
using ECS;
using MVC;
using System;
using System.Threading;
using DCL.CharacterCamera;
using DCL.FeatureFlags;
using DCL.InWorldCamera;
using DCL.UI.Buttons;
using ECS.Abstract;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private const string SOURCE_BUTTON = "Button";

        private readonly IMVCManager mvcManager;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly SmartWearablesSideBarTooltipController smartWearablesTooltipController;
        private readonly IWebBrowser webBrowser;
        private readonly IChatHistory chatHistory;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly URLBuilder urlBuilder = new ();
        private readonly World globalWorld;
        private readonly URLParameter marketplaceSourceParam = new ("utm_source", "sidebar");
        private readonly ChatEventBus chatEventBus;
        private readonly IDisposable chatEventBusSubscription;
        private readonly bool isCameraReelFeatureEnabled;
        private readonly bool isFriendsFeatureEnabled;
        private readonly bool isDiscoverFeatureEnabled;

        private CancellationTokenSource checkForMarketplaceCreditsFeatureCts = new ();
        private CancellationTokenSource referralNotificationCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();
        private CancellationTokenSource openPanelCts = new ();
        private SingleInstanceEntity? cameraInternal;
        private bool isMarketplaceCreditsFeatureEnabled;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.PERSISTENT;

        private SingleInstanceEntity? camera => cameraInternal ??= globalWorld.CacheCamera();

        public event Action? HelpOpened;

        public SidebarController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            SidebarProfileButtonPresenter profileButtonPresenter,
            SmartWearablesSideBarTooltipController smartWearablesTooltipController,
            IWebBrowser webBrowser,
            IChatHistory chatHistory,
            ISelfProfile selfProfile,
            IRealmData realmData,
            IDecentralandUrlsSource decentralandUrlsSource,
            World globalWorld,
            ChatEventBus chatEventBus)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileButtonPresenter = profileButtonPresenter;
            this.smartWearablesTooltipController = smartWearablesTooltipController;
            this.webBrowser = webBrowser;
            this.chatHistory = chatHistory;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.globalWorld = globalWorld;
            this.chatEventBus = chatEventBus;
            isCameraReelFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CAMERA_REEL);
            isFriendsFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS);
            isMarketplaceCreditsFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.MARKETPLACE_CREDITS);
            isDiscoverFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.DISCOVER_PLACES);

            chatEventBusSubscription = chatEventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            chatEventBusSubscription.Dispose();
            chatHistory.ReadMessagesChanged -= OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded -= OnChatHistoryMessageAdded;

            if (viewInstance != null)
            {
                viewInstance.settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
                viewInstance.communitiesButton.onClick.RemoveListener(OnCommunitiesButtonClicked);
                viewInstance.mapButton.onClick.RemoveListener(OnMapButtonClicked);
                viewInstance.autoHideToggle.onValueChanged.RemoveListener(OnAutoHideToggleChanged);
                viewInstance.helpButton.onClick.RemoveListener(OnHelpButtonClicked);
                viewInstance.controlsButton.onClick.RemoveListener(OnControlsButtonClicked);
                viewInstance.InWorldCameraButton.onClick.RemoveListener(OnOpenCameraButtonClicked);
                viewInstance.marketplaceButton.onClick.RemoveListener(OnMarketplaceButtonClicked);
                viewInstance.emotesWheelButton.onClick.RemoveListener(OnEmotesWheelButtonClicked);
                viewInstance.NotificationsButton.onClick.RemoveListener(OpenNotificationsPanel);
                viewInstance.skyboxButton.onClick.RemoveListener(OpenSkyboxSettingsPanel);
                viewInstance.ProfileWidget.OpenProfileButton.onClick.RemoveListener(OnProfilePanelButtonClicked);
                viewInstance.sidebarConfigButton.onClick.RemoveListener(OnSidebarConfigButtonClicked);
                viewInstance.unreadMessagesButton.onClick.RemoveListener(OnUnreadMessagesButtonClicked);
                viewInstance.backpackButton.onClick.RemoveListener(OnBackpackButtonClicked);
                viewInstance.smartWearablesButton.OnButtonHover -= OnSmartWearablesButtonHover;
                viewInstance.smartWearablesButton.OnButtonUnhover -= OnSmartWearablesButtonUnhover;

                if (isCameraReelFeatureEnabled)
                    viewInstance.cameraReelButton.onClick.RemoveListener(OnCameraReelButtonClicked);

                if (isFriendsFeatureEnabled)
                    viewInstance.friendsButton.onClick.RemoveListener(OnFriendsButtonClicked);

                if (isMarketplaceCreditsFeatureEnabled)
                    viewInstance.marketplaceCreditsButton.onClick.RemoveListener(OnMarketplaceCreditsButtonClicked);
            }

            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            checkForMarketplaceCreditsFeatureCts.SafeCancelAndDispose();
            referralNotificationCts.SafeCancelAndDispose();
            checkForCommunitiesFeatureCts.SafeCancelAndDispose();
            openPanelCts.SafeCancelAndDispose();
        }

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent eventData) =>
            OnChatViewFoldingChanged(eventData.CurrentState is not HiddenChatState && eventData.CurrentState is not MinimizedChatState);

        protected override void OnViewInstantiated()
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
            viewInstance.skyboxButton.Button.interactable = true;
            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(isFriendsFeatureEnabled);
            viewInstance.cameraReelButton.gameObject.SetActive(isCameraReelFeatureEnabled);
            viewInstance.InWorldCameraButton.gameObject.SetActive(isCameraReelFeatureEnabled);
            viewInstance.placesButton.gameObject.SetActive(isDiscoverFeatureEnabled);
            viewInstance.eventsButton.gameObject.SetActive(isDiscoverFeatureEnabled);

            SubscribeToEvents();

            checkForMarketplaceCreditsFeatureCts = checkForMarketplaceCreditsFeatureCts.SafeRestart();
            CheckForMarketplaceCreditsFeatureAsync(checkForMarketplaceCreditsFeatureCts.Token).Forget();

            checkForCommunitiesFeatureCts = checkForCommunitiesFeatureCts.SafeRestart();
            CheckForCommunitiesFeatureAsync(checkForCommunitiesFeatureCts.Token).Forget();

            OnChatViewFoldingChanged(true);
        }

        private void SubscribeToEvents()
        {
            chatHistory.ReadMessagesChanged += OnChatHistoryReadMessagesChanged;
            chatHistory.MessageAdded += OnChatHistoryMessageAdded;

            mvcManager.OnViewShowed += OnMvcManagerViewShowed;
            mvcManager.OnViewClosed += OnMvcManagerViewClosed;

            viewInstance!.settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            viewInstance.communitiesButton.onClick.AddListener(OnCommunitiesButtonClicked);
            viewInstance.mapButton.onClick.AddListener(OnMapButtonClicked);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.InWorldCameraButton.onClick.AddListener(OnOpenCameraButtonClicked);
            viewInstance.marketplaceButton.onClick.AddListener(OnMarketplaceButtonClicked);

            viewInstance.emotesWheelButton.onClick.AddListener(OnEmotesWheelButtonClicked);
            viewInstance.NotificationsButton.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.skyboxButton.onClick.AddListener(OpenSkyboxSettingsPanel);
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OnProfilePanelButtonClicked);
            viewInstance.sidebarConfigButton.onClick.AddListener(OnSidebarConfigButtonClicked);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);

            viewInstance.backpackButton.onClick.AddListener(OnBackpackButtonClicked);
            viewInstance.smartWearablesButton.OnButtonHover += OnSmartWearablesButtonHover;
            viewInstance.smartWearablesButton.OnButtonUnhover += OnSmartWearablesButtonUnhover;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            if (isCameraReelFeatureEnabled) viewInstance.cameraReelButton.onClick.AddListener(OnCameraReelButtonClicked);
            if (isFriendsFeatureEnabled) viewInstance.friendsButton.onClick.AddListener(OnFriendsButtonClicked);

            if (isDiscoverFeatureEnabled)
            {
                viewInstance.placesButton?.onClick.AddListener(OnPlacesButtonClicked);
                viewInstance.eventsButton.onClick.AddListener(OnEventsButtonClicked);
            }
        }

        private void OnMvcManagerViewClosed(IController closedController)
        {
            if (!viewInstance) return;

            //When panels are closed through shortcuts we need to be able to de-select the buttons remotely.
            switch (closedController)
            {
                case EmotesWheelController:
                    viewInstance.emotesWheelButton.Deselect(); break;
                case FriendsPanelController:
                    viewInstance.friendsButton.Deselect(); break;
            }
        }

        private void OnMvcManagerViewShowed(IController showedController)
        {
            if (!viewInstance) return;

            // Panels that are controllers and can be opened using shortcuts, we need to select their buttons remotely.
            switch (showedController)
            {
                case EmotesWheelController:
                    viewInstance.emotesWheelButton.Select();
                    break;
                case FriendsPanelController:
                    viewInstance.friendsButton.Select();
                    break;
            }
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

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)  =>
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;

        private void OnChatViewFoldingChanged(bool isUnfolded)
        {
            if (isUnfolded)
                viewInstance?.unreadMessagesButton.Select();
            else
                viewInstance?.unreadMessagesButton.Deselect();
        }

        private void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel) =>
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;

        private void OnAutoHideToggleChanged(bool value) => viewInstance?.SetAutoHideSidebarStatus(value);
        private void OnRewardNotificationClicked(object[] parameters) => viewInstance!.backpackNotificationIndicator.SetActive(false);
        private void OnRewardNotificationReceived(INotification newNotification) => viewInstance!.backpackNotificationIndicator.SetActive(true);

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

            isMarketplaceCreditsFeatureEnabled = MarketplaceCreditsUtils.IsUserAllowedToUseTheFeatureAsync(ownProfile.UserId, ct);
            viewInstance?.marketplaceCreditsButton.gameObject.SetActive(isMarketplaceCreditsFeatureEnabled);

            if (isMarketplaceCreditsFeatureEnabled)
                viewInstance?.marketplaceCreditsButton.onClick.AddListener(OnMarketplaceCreditsButtonClicked);
        }

        private async UniTaskVoid CheckForCommunitiesFeatureAsync(CancellationToken ct)
        {
            viewInstance?.communitiesButton.gameObject.SetActive(false);
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);
            viewInstance?.communitiesButton.gameObject.SetActive(includeCommunities);
        }

#region Sidebar button handlers
        private void OnOpenCameraButtonClicked()
        {
            if (globalWorld.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled && !globalWorld.Has<ToggleInWorldCameraRequest>(camera!.Value))
                globalWorld.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = !globalWorld.Has<InWorldCameraComponent>(camera!.Value), Source = SOURCE_BUTTON });
        }

        private void OnSettingsButtonClicked() => OpenExplorePanelInSection(ExploreSections.Settings);
        private void OnCommunitiesButtonClicked() => OpenExplorePanelInSection(ExploreSections.Communities);
        private void OnMapButtonClicked() => OpenExplorePanelInSection(ExploreSections.Navmap);
        private void OnCameraReelButtonClicked() => OpenExplorePanelInSection(ExploreSections.CameraReel);
        private void OnPlacesButtonClicked() => OpenExplorePanelInSection(ExploreSections.Places);
        private void OnEventsButtonClicked() => OpenExplorePanelInSection(ExploreSections.Events);

        private void OnBackpackButtonClicked()
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
            OpenExplorePanelInSection(ExploreSections.Backpack);
        }
        private void OnUnreadMessagesButtonClicked() => chatEventBus.RaiseToggleChatEvent();
        private void OnEmotesWheelButtonClicked() => OpenPanelAsync(viewInstance!.emotesWheelButton, EmotesWheelController.IssueCommand()).Forget();
        private void OnFriendsButtonClicked() =>
            OpenPanelAsync(viewInstance!.friendsButton, FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS))).Forget();

        private void OnMarketplaceCreditsButtonClicked() =>
            OpenPanelAsync(viewInstance!.sidebarConfigButton,
                MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false))).Forget();

        private void OnHelpButtonClicked()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            HelpOpened?.Invoke();
        }

        private void OnControlsButtonClicked() => OpenPanelAsync(null, ControlsPanelController.IssueCommand()).Forget();
        private void OnSidebarConfigButtonClicked() => OpenPanelAsync(viewInstance!.sidebarConfigButton, SidebarSettingsWidgetController.IssueCommand()).Forget();
        private void OnProfilePanelButtonClicked() => OpenPanelAsync(null, ProfileMenuController.IssueCommand()).Forget();
        private void OpenSkyboxSettingsPanel() => OpenPanelAsync(viewInstance!.skyboxButton, SkyboxMenuController.IssueCommand()).Forget();
        private void OpenNotificationsPanel() => OpenPanelAsync(viewInstance!.NotificationsButton, NotificationsPanelController.IssueCommand()).Forget();

        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar) =>
            OpenPanelAsync(null, ExplorePanelController.IssueCommand(new ExplorePanelParameter(section, backpackSection))).Forget();

        private void OnSmartWearablesButtonHover() => OpenPanelAsync(viewInstance!.smartWearablesButton, SmartWearablesSideBarTooltipController.IssueCommand()).Forget();
        private void OnSmartWearablesButtonUnhover() => smartWearablesTooltipController.Close();

        private void OnMarketplaceButtonClicked()
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Market)));
            urlBuilder.AppendParameter(marketplaceSourceParam);
            webBrowser.OpenUrl(urlBuilder.Build());
        }

        private async UniTaskVoid OpenPanelAsync<TView, TInputData>(
            ISelectableButton? button,
            ShowCommand<TView, TInputData> command)
            where TView: IView
        {
            if (!viewInstance) return;

            openPanelCts = openPanelCts.SafeRestart();
            CancellationToken ct = openPanelCts.Token;

            viewInstance.BlockSidebar();
            button?.Select();

            try { await mvcManager.ShowAsync(command, ct); }
            catch (OperationCanceledException){} // Expected cancellation when a new panel is opened
            catch (Exception e) { ReportHub.LogException(new Exception("Exception on opening panel from Sidebar: " + e.Message, e), ReportCategory.UI); }
            finally
            {
                if (viewInstance != null)
                {
                    button?.Deselect();
                    viewInstance.UnblockSidebar();
                }
            }
        }

#endregion
    }
}
