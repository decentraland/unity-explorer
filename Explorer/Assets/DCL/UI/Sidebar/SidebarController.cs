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
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly IChatHistory chatHistory;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly URLBuilder urlBuilder = new ();
        private readonly World globalWorld;
        private readonly URLParameter marketplaceSourceParam = new ("utm_source", "sidebar");
        private readonly ChatEventBus chatEventBus;
        private readonly IDisposable? chatEventBusSubscription;
        private bool includeMarketplaceCredits;
        private CancellationTokenSource checkForMarketplaceCreditsFeatureCts = new ();
        private CancellationTokenSource? referralNotificationCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();
        private SingleInstanceEntity? cameraInternal;

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
            includeCameraReel = FeaturesRegistry.Instance.IsEnabled(FeatureId.CAMERA_REEL);
            includeFriends = FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS);
            includeMarketplaceCredits = FeaturesRegistry.Instance.IsEnabled(FeatureId.MARKETPLACE_CREDITS);

            chatEventBusSubscription = chatEventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            chatEventBusSubscription?.Dispose();
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
                viewInstance.marketplaceButton.onClick.RemoveListener(OpenMarketplace);
                viewInstance.emotesWheelButton.onClick.RemoveListener(OnEmotesWheelButtonClicked);
                viewInstance.NotificationsButton.onClick.RemoveListener(OpenNotificationsPanel);
                viewInstance.skyboxButton.onClick.RemoveListener(OpenSkyboxSettingsPanel);
                viewInstance.ProfileWidget.OpenProfileButton.onClick.RemoveListener(OpenProfilePanel);
                viewInstance.sidebarConfigButton.onClick.RemoveListener(OpenSidebarSettings);
                viewInstance.unreadMessagesButton.onClick.RemoveListener(OnUnreadMessagesButtonClicked);
                viewInstance.backpackButton.onClick.RemoveListener(OnBackpackButtonClicked);
                viewInstance.smartWearablesButton.OnButtonHover -= OnSmartWearablesButtonHover;
                viewInstance.smartWearablesButton.OnButtonUnhover -= OnSmartWearablesButtonUnhover;

                if (includeCameraReel)
                    viewInstance.cameraReelButton.onClick.RemoveListener(OnCameraReelButtonClicked);

                if (includeFriends)
                    viewInstance.friendsButton.onClick.RemoveListener(OnFriendsButtonClicked);

                if (includeMarketplaceCredits)
                    viewInstance.marketplaceCreditsButton.onClick.RemoveListener(OnMarketplaceCreditsButtonClicked);
            }

            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.UnsubscribeFromNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            checkForMarketplaceCreditsFeatureCts.SafeCancelAndDispose();
            referralNotificationCts.SafeCancelAndDispose();
            checkForCommunitiesFeatureCts.SafeCancelAndDispose();
        }

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent eventData) =>
            OnChatViewFoldingChanged(eventData.CurrentState is not HiddenChatState && eventData.CurrentState is not MinimizedChatState);

        protected override void OnViewInstantiated()
        {
            viewInstance!.backpackNotificationIndicator.SetActive(false);
            viewInstance.skyboxButton.Button.interactable = true;
            viewInstance.PersistentFriendsPanelOpener.gameObject.SetActive(includeFriends);
            viewInstance.cameraReelButton.gameObject.SetActive(includeCameraReel);
            viewInstance.InWorldCameraButton.gameObject.SetActive(includeCameraReel);

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

            viewInstance!.settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            viewInstance.communitiesButton.onClick.AddListener(OnCommunitiesButtonClicked);
            viewInstance.mapButton.onClick.AddListener(OnMapButtonClicked);
            viewInstance.autoHideToggle.onValueChanged.AddListener(OnAutoHideToggleChanged);
            viewInstance.helpButton.onClick.AddListener(OnHelpButtonClicked);
            viewInstance.controlsButton.onClick.AddListener(OnControlsButtonClicked);
            viewInstance.InWorldCameraButton.onClick.AddListener(OnOpenCameraButtonClicked);
            viewInstance.marketplaceButton.onClick.AddListener(OpenMarketplace);

            viewInstance.emotesWheelButton.onClick.AddListener(OnEmotesWheelButtonClicked);
            viewInstance.NotificationsButton.onClick.AddListener(OpenNotificationsPanel);
            viewInstance.skyboxButton.onClick.AddListener(OpenSkyboxSettingsPanel);
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfilePanel);
            viewInstance.sidebarConfigButton.onClick.AddListener(OpenSidebarSettings);
            viewInstance.unreadMessagesButton.onClick.AddListener(OnUnreadMessagesButtonClicked);

            viewInstance.backpackButton.onClick.AddListener(OnBackpackButtonClicked);
            viewInstance.smartWearablesButton.OnButtonHover += OnSmartWearablesButtonHover;
            viewInstance.smartWearablesButton.OnButtonUnhover += OnSmartWearablesButtonUnhover;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationReceived);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardNotificationClicked);
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);

            if (includeCameraReel) viewInstance.cameraReelButton.onClick.AddListener(OnCameraReelButtonClicked);
            if (includeFriends) viewInstance.friendsButton.onClick.AddListener(OnFriendsButtonClicked);
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

        private void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)
        {
            viewInstance!.chatUnreadMessagesNumber.Number = chatHistory.TotalMessages - chatHistory.ReadMessages;
        }

        private void OnChatViewFoldingChanged(bool isUnfolded)
        {
            if (isUnfolded)
                viewInstance?.unreadMessagesButton.Select();
            else
                viewInstance?.unreadMessagesButton.Deselect();
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
                viewInstance?.marketplaceCreditsButton.onClick.AddListener(OnMarketplaceCreditsButtonClicked);
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
            chatEventBus.RaiseToggleChatEvent();
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
                    mvcManager.ShowAsync(MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false))))
               .Forget();
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

        private void OpenMarketplace()
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Market)));
            urlBuilder.AppendParameter(marketplaceSourceParam);
            webBrowser.OpenUrl(urlBuilder.Build());
        }
#endregion
    }
}
