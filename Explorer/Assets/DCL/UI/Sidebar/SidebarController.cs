using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Audio;
using DCL.BugReporting.UI;
using DCL.Chat;
using DCL.Chat.ChatStates;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.InWorldCamera;
using DCL.Friends.UI.FriendPanel;
using DCL.MarketplaceCredits;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Prefs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Buttons;
using DCL.UI.Controls;
using DCL.UI.ProfileElements;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.VoiceChat.UI;
using DCL.Web3.Identities;
using ECS;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private const string SOURCE_BUTTON = "Button";
        private const string MASTER_VOLUME_PARAM = "Master_Volume";
        private const string WORLD_VOLUME_PARAM = "World_Volume";
        private const string MUSIC_VOLUME_PARAM = "Music_Volume";
        private const string UI_VOLUME_PARAM = "UI_Volume";
        private const string AVATAR_VOLUME_PARAM = "Avatar_Volume";
        private const string SHOP_ITEMS_PATH = "items";
        private const string SHOP_CATEGORY_PARAM = "category";
        private const string EMOTE_CATEGORY = "emote";
        private const string WEARABLE_CATEGORY = "wearable";

        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache identityCache;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly SelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly URLBuilder urlBuilder = new ();
        private readonly IDisposable chatEventBusSubscription;
        private readonly bool isFriendsFeatureEnabled;
        private readonly bool isNearbyVoiceChatEnabled;
        private readonly bool isDiscoverFeatureEnabled;
        private readonly bool isBugReportFeatureEnabled;
        private readonly SupportRequestService supportRequestService;
        private readonly InWorldCameraToggle cameraToggle;
        private readonly AudioMixer generalAudioMixer;
        private readonly VolumeBus volumeBus;
        private readonly List<AudioSliderBinder> audioSliderBinders = new ();
        private readonly bool isCameraReelFeatureEnabled;

        private readonly CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource checkForMarketplaceCreditsFeatureCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();
        private CancellationTokenSource referralNotificationCts = new ();
        private CancellationTokenSource openPanelCts = new ();
        private bool isMarketplaceCreditsFeatureEnabled;

        private SimpleHoverableButton? openMenuButton;
        private GameObject? openMenu;
        private MenuElementView? highlightedOption;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? PlacesOpened;
        public event Action? EventsOpened;

        public SidebarController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            SidebarProfileButtonPresenter profileButtonPresenter,
            UnityAppWebBrowser webBrowser,
            SelfProfile selfProfile,
            IRealmData realmData,
            IDecentralandUrlsSource decentralandUrlsSource,
            ChatEventBus chatEventBus,
            SupportRequestService supportRequestService,
            InWorldCameraToggle cameraToggle,
            AudioMixer generalAudioMixer,
            VolumeBus volumeBus,
            IWeb3IdentityCache identityCache)
            : base(viewFactory)
        {
            this.supportRequestService = supportRequestService;
            this.cameraToggle = cameraToggle;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;
            this.mvcManager = mvcManager;
            this.identityCache = identityCache;
            this.profileButtonPresenter = profileButtonPresenter;
            this.webBrowser = webBrowser;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.decentralandUrlsSource = decentralandUrlsSource;
            isFriendsFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.Friends);
            isMarketplaceCreditsFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.MarketplaceCredits);
            isNearbyVoiceChatEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.NearbyVoiceChat);
            isDiscoverFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.Discover);
            isBugReportFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.BugReport);
            isCameraReelFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CameraReel);

            chatEventBusSubscription = chatEventBus.Subscribe<ChatEvents.ChatStateChangedEvent>(OnChatStateChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            chatEventBusSubscription.Dispose();

            mvcManager.OnViewShowed -= OnMvcManagerViewShowed;
            mvcManager.OnViewClosed -= OnMvcManagerViewClosed;

            if (viewInstance != null)
            {
                viewInstance.MapButton.onClick.RemoveListener(OnMapButtonClicked);
                viewInstance.DiscoverButton.onClick.RemoveListener(OnDiscoverButtonClicked);
                viewInstance.ShopButton.onClick.RemoveListener(OnShopButtonClicked);
                viewInstance.SettingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
                viewInstance.AudioSettingsButton.onClick.RemoveListener(OnAudioSettingsButtonClicked);
                viewInstance.GalleryButton.onClick.RemoveListener(OnGalleryButtonClicked);
                viewInstance.NotificationsButton.onClick.RemoveListener(OnNotificationsButtonClicked);
                viewInstance.ProfileWidget.OpenProfileButton.onClick.RemoveListener(OnProfileButtonClicked);
                viewInstance.EmotesButton.onClick.RemoveListener(OnEmotesButtonClicked);

                if (isFriendsFeatureEnabled)
                    viewInstance.FriendsButton.onClick.RemoveListener(OnFriendsButtonClicked);

                if (isNearbyVoiceChatEnabled)
                    viewInstance.NearbyVoiceChatButton.Button.onClick.RemoveListener(OnNearbyVoiceButtonClicked);

                if (isMarketplaceCreditsFeatureEnabled)
                    viewInstance.MarketplaceCreditsButton.onClick.RemoveListener(OnMarketplaceCreditsButtonClicked);

                UnsubscribeFromOption(viewInstance.discoverPlacesOption, OnPlacesOptionClicked);
                UnsubscribeFromOption(viewInstance.discoverEventsOption, OnEventsOptionClicked);
                UnsubscribeFromOption(viewInstance.discoverCommunitiesOption, OnCommunitiesOptionClicked);
                viewInstance.discoverCloseButton.onClick.RemoveListener(CloseOpenMenu);

                UnsubscribeFromOption(viewInstance.galleryGalleryOption, OnGalleryOptionClicked);
                UnsubscribeFromOption(viewInstance.galleryTakeShotOption, OnTakeShotOptionClicked);
                viewInstance.galleryCloseButton.onClick.RemoveListener(CloseOpenMenu);
                viewInstance.audioCloseButton.onClick.RemoveListener(CloseOpenMenu);

                viewInstance.shopEmotesBanner.onClick.RemoveListener(OnExploreEmotesClicked);
                viewInstance.shopWearablesBanner.onClick.RemoveListener(OnExploreWearablesClicked);
                viewInstance.shopCloseButton.onClick.RemoveListener(CloseOpenMenu);

                UnsubscribeFromOption(viewInstance.systemSettingsOption, OnSettingsOptionClicked);
                UnsubscribeFromOption(viewInstance.systemControlsOption, OnControlsOptionClicked);
                UnsubscribeFromOption(viewInstance.systemContactSupportOption, OnContactSupportOptionClicked);
                UnsubscribeFromOption(viewInstance.systemDiscordOption, OnDiscordOptionClicked);
                UnsubscribeFromOption(viewInstance.systemFaqOption, OnFaqOptionClicked);
                viewInstance.systemCloseButton.onClick.RemoveListener(CloseOpenMenu);

                if (isBugReportFeatureEnabled)
                    UnsubscribeFromOption(viewInstance.systemReportBugOption, OnReportBugOptionClicked);
            }

            foreach (AudioSliderBinder binder in audioSliderBinders)
                binder.Dispose();

            profileWidgetCts.SafeCancelAndDispose();
            checkForCommunitiesFeatureCts.SafeCancelAndDispose();
            checkForMarketplaceCreditsFeatureCts.SafeCancelAndDispose();
            referralNotificationCts.SafeCancelAndDispose();
            openPanelCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.PersistentFriendsPanelOpener.gameObject.SetActive(isFriendsFeatureEnabled);
            viewInstance.FriendsButton.gameObject.SetActive(isFriendsFeatureEnabled);
            viewInstance.NearbyVoiceChatButton.gameObject.SetActive(isNearbyVoiceChatEnabled);
            viewInstance.discoverPlacesOption.gameObject.SetActive(isDiscoverFeatureEnabled);
            viewInstance.discoverEventsOption.gameObject.SetActive(isDiscoverFeatureEnabled);
            viewInstance.systemReportBugOption.gameObject.SetActive(isBugReportFeatureEnabled);

            // Both gallery options come from the same feature, so the whole entry goes away rather than opening an empty menu.
            viewInstance.GalleryButton.gameObject.SetActive(isCameraReelFeatureEnabled);

            BindAudioSliders();
            CloseAllMenus();
            SubscribeToEvents();

            checkForMarketplaceCreditsFeatureCts = checkForMarketplaceCreditsFeatureCts.SafeRestart();
            CheckForMarketplaceCreditsFeatureAsync(checkForMarketplaceCreditsFeatureCts.Token).Forget();

            checkForCommunitiesFeatureCts = checkForCommunitiesFeatureCts.SafeRestart();
            CheckForCommunitiesFeatureAsync(checkForCommunitiesFeatureCts.Token).Forget();

            OnChatViewFoldingChanged(true);
        }

        protected override void OnViewShow()
        {
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

        private void SubscribeToEvents()
        {
            mvcManager.OnViewShowed += OnMvcManagerViewShowed;
            mvcManager.OnViewClosed += OnMvcManagerViewClosed;

            viewInstance!.MapButton.onClick.AddListener(OnMapButtonClicked);
            viewInstance.DiscoverButton.onClick.AddListener(OnDiscoverButtonClicked);
            viewInstance.ShopButton.onClick.AddListener(OnShopButtonClicked);
            viewInstance.SettingsButton.onClick.AddListener(OnSettingsButtonClicked);
            viewInstance.AudioSettingsButton.onClick.AddListener(OnAudioSettingsButtonClicked);
            viewInstance.GalleryButton.onClick.AddListener(OnGalleryButtonClicked);
            viewInstance.NotificationsButton.onClick.AddListener(OnNotificationsButtonClicked);
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OnProfileButtonClicked);
            viewInstance.EmotesButton.onClick.AddListener(OnEmotesButtonClicked);

            SubscribeToOption(viewInstance.discoverPlacesOption, OnPlacesOptionClicked);
            SubscribeToOption(viewInstance.discoverEventsOption, OnEventsOptionClicked);
            SubscribeToOption(viewInstance.discoverCommunitiesOption, OnCommunitiesOptionClicked);
            viewInstance.discoverCloseButton.onClick.AddListener(CloseOpenMenu);

            SubscribeToOption(viewInstance.galleryGalleryOption, OnGalleryOptionClicked);
            SubscribeToOption(viewInstance.galleryTakeShotOption, OnTakeShotOptionClicked);
            viewInstance.galleryCloseButton.onClick.AddListener(CloseOpenMenu);
            viewInstance.audioCloseButton.onClick.AddListener(CloseOpenMenu);

            viewInstance.shopEmotesBanner.onClick.AddListener(OnExploreEmotesClicked);
            viewInstance.shopWearablesBanner.onClick.AddListener(OnExploreWearablesClicked);
            viewInstance.shopCloseButton.onClick.AddListener(CloseOpenMenu);

            SubscribeToOption(viewInstance.systemSettingsOption, OnSettingsOptionClicked);
            SubscribeToOption(viewInstance.systemControlsOption, OnControlsOptionClicked);
            SubscribeToOption(viewInstance.systemContactSupportOption, OnContactSupportOptionClicked);
            SubscribeToOption(viewInstance.systemDiscordOption, OnDiscordOptionClicked);
            SubscribeToOption(viewInstance.systemFaqOption, OnFaqOptionClicked);
            viewInstance.systemCloseButton.onClick.AddListener(CloseOpenMenu);

            if (isBugReportFeatureEnabled)
                SubscribeToOption(viewInstance.systemReportBugOption, OnReportBugOptionClicked);

            if (isFriendsFeatureEnabled)
                viewInstance.FriendsButton.onClick.AddListener(OnFriendsButtonClicked);

            if (isNearbyVoiceChatEnabled)
                viewInstance.NearbyVoiceChatButton.Button.onClick.AddListener(OnNearbyVoiceButtonClicked);

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.REFERRAL_NEW_TIER_REACHED, OnReferralNewTierNotificationClicked);
        }

#region Nav rail menus

        /// <summary>
        ///     Opens the menu nested under the clicked button, closing whichever menu was open before it.
        ///     Clicking the button whose menu is already open closes it.
        /// </summary>
        private void ToggleMenu(SimpleHoverableButton? button, GameObject menu)
        {
            bool shouldOpen = openMenu != menu;

            // Closes any MVC panel opened from the rail, so only one entry is ever selected.
            openPanelCts = openPanelCts.SafeRestart();

            CloseOpenMenu();

            if (!shouldOpen) return;

            button?.SetSelected(true);
            menu.SetActive(true);

            openMenuButton = button;
            openMenu = menu;

            viewInstance!.BlockSidebar();
        }

        private void CloseOpenMenu()
        {
            if (openMenu == null) return;

            openMenuButton?.SetSelected(false);
            openMenu.SetActive(false);

            openMenuButton = null;
            openMenu = null;

            ClearHighlightedOption();

            viewInstance!.UnblockSidebar();
        }

#endregion

#region Menu options

        private void SubscribeToOption(MenuElementView option, UnityAction onClick)
        {
            option.PointerEntered += OnOptionPointerEntered;
            option.PointerExited += OnOptionPointerExited;
            option.Button.onClick.AddListener(onClick);
        }

        private void UnsubscribeFromOption(MenuElementView option, UnityAction onClick)
        {
            option.PointerEntered -= OnOptionPointerEntered;
            option.PointerExited -= OnOptionPointerExited;
            option.Button.onClick.RemoveListener(onClick);
        }

        /// <summary>
        ///     The highlight is owned here rather than by the row, so only one row in the whole rail can carry it
        ///     even if a pointer-exit is missed.
        /// </summary>
        private void OnOptionPointerEntered(MenuElementView option)
        {
            if (highlightedOption == option) return;

            highlightedOption?.SetSelected(false);
            highlightedOption = option;
            option.SetSelected(true);
        }

        private void OnOptionPointerExited(MenuElementView option)
        {
            if (highlightedOption != option) return;

            ClearHighlightedOption();
        }

        private void ClearHighlightedOption()
        {
            highlightedOption?.SetSelected(false);
            highlightedOption = null;
        }

        private void OnPlacesOptionClicked()
        {
            PlacesOpened?.Invoke();
            OpenExplorePanelInSection(ExploreSections.Places);
        }

        private void OnEventsOptionClicked()
        {
            EventsOpened?.Invoke();
            OpenExplorePanelInSection(ExploreSections.Events);
        }

        private void OnCommunitiesOptionClicked() => OpenExplorePanelInSection(ExploreSections.Communities);

        private void OnExploreEmotesClicked() => OpenShopCategory(EMOTE_CATEGORY);

        private void OnExploreWearablesClicked() => OpenShopCategory(WEARABLE_CATEGORY);

        private void OpenShopCategory(string category)
        {
            CloseOpenMenu();

            urlBuilder.Clear();

            URLAddress url = urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ShopLink)))
                                       .AppendPath(URLPath.FromString(SHOP_ITEMS_PATH))
                                       .AppendParameter(new URLParameter(SHOP_CATEGORY_PARAM, category))
                                       .Build();

            webBrowser.OpenUrlMainThreadOnly(url);
        }

        private void BindAudioSliders()
        {
            audioSliderBinders.Add(new AudioSliderBinder(viewInstance!.audioMasterSlider, generalAudioMixer, volumeBus.SetMasterVolume,
                new AudioSliderBinder.Channel(MASTER_VOLUME_PARAM, DCLPrefKeys.SETTINGS_MASTER_VOLUME)));

            audioSliderBinders.Add(new AudioSliderBinder(viewInstance.audioInWorldSlider, generalAudioMixer, volumeBus.SetWorldVolume,
                new AudioSliderBinder.Channel(WORLD_VOLUME_PARAM, DCLPrefKeys.SETTINGS_WORLD_VOLUME),
                new AudioSliderBinder.Channel(MUSIC_VOLUME_PARAM, DCLPrefKeys.SETTINGS_MUSIC_VOLUME)));

            audioSliderBinders.Add(new AudioSliderBinder(viewInstance.audioUiSlider, generalAudioMixer, null,
                new AudioSliderBinder.Channel(UI_VOLUME_PARAM, DCLPrefKeys.SETTINGS_UI_VOLUME)));

            audioSliderBinders.Add(new AudioSliderBinder(viewInstance.audioAvatarSlider, generalAudioMixer, null,
                new AudioSliderBinder.Channel(AVATAR_VOLUME_PARAM, DCLPrefKeys.SETTINGS_AVATAR_VOLUME)));
        }

        private void RefreshAudioSliders()
        {
            foreach (AudioSliderBinder binder in audioSliderBinders)
                binder.Refresh();
        }

        private void OnGalleryOptionClicked() => OpenExplorePanelInSection(ExploreSections.CameraReel);

        private void OnTakeShotOptionClicked()
        {
            CloseOpenMenu();
            cameraToggle.Toggle(SOURCE_BUTTON);
        }

        private void OnSettingsOptionClicked() => OpenExplorePanelInSection(ExploreSections.Settings);

        private void OnControlsOptionClicked() => OpenPanelAsync(null, ControlsPanelController.IssueCommand()).Forget();

        private void OnReportBugOptionClicked() => OpenPanelAsync(null, BugReportController.IssueCommand(new BugReportParams())).Forget();

        private void OnContactSupportOptionClicked()
        {
            CloseOpenMenu();
            supportRequestService.OpenSupport();
        }

        private void OnDiscordOptionClicked() => OpenWebUrl(DecentralandUrl.Discord);

        private void OnFaqOptionClicked() => OpenWebUrl(DecentralandUrl.Faqs);

        private void OpenWebUrl(DecentralandUrl url)
        {
            CloseOpenMenu();
            webBrowser.OpenUrlMainThreadOnly(url);
        }

        private void OpenExplorePanelInSection(ExploreSections section) =>
            OpenPanelAsync(null, ExplorePanelController.IssueCommand(new ExplorePanelParameter(section))).Forget();

        private async UniTaskVoid CheckForCommunitiesFeatureAsync(CancellationToken ct)
        {
            viewInstance?.discoverCommunitiesOption.gameObject.SetActive(false);
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);
            viewInstance?.discoverCommunitiesOption.gameObject.SetActive(includeCommunities);
        }

        private void CloseAllMenus()
        {
            viewInstance!.discoverMenu.SetActive(false);
            viewInstance.shopMenu.SetActive(false);
            viewInstance.systemMenu.SetActive(false);
            viewInstance.audioMenu.SetActive(false);
            viewInstance.galleryMenu.SetActive(false);
            viewInstance.profileMenu.SetActive(false);

            // The minimap keeps whatever visibility the prefab was authored with; the Map button only mirrors it.
            viewInstance.MapButton.SetSelected(viewInstance.minimapMenu.activeSelf);

            viewInstance.DiscoverButton.SetSelected(false);
            viewInstance.ShopButton.SetSelected(false);
            viewInstance.SettingsButton.SetSelected(false);
            viewInstance.AudioSettingsButton.SetSelected(false);
            viewInstance.GalleryButton.SetSelected(false);
            viewInstance.NotificationsButton.SetSelected(false);
            viewInstance.FriendsButton.SetSelected(false);
            viewInstance.EmotesButton.SetSelected(false);

            ClearHighlightedOption();

            openMenuButton = null;
            openMenu = null;
        }

        /// <summary>
        ///     The minimap stays usable alongside any other menu, so it is toggled outside the single-menu group.
        /// </summary>
        private void OnMapButtonClicked()
        {
            bool show = !viewInstance!.MapButton.IsSelected;

            viewInstance.MapButton.SetSelected(show);
            viewInstance.minimapMenu.SetActive(show);
        }

        private void OnDiscoverButtonClicked() => ToggleMenu(viewInstance!.DiscoverButton, viewInstance.discoverMenu);
        private void OnShopButtonClicked() => ToggleMenu(viewInstance!.ShopButton, viewInstance.shopMenu);
        private void OnSettingsButtonClicked() => ToggleMenu(viewInstance!.SettingsButton, viewInstance.systemMenu);
        private void OnAudioSettingsButtonClicked()
        {
            RefreshAudioSliders();
            ToggleMenu(viewInstance!.AudioSettingsButton, viewInstance.audioMenu);
        }
        private void OnGalleryButtonClicked() => ToggleMenu(viewInstance!.GalleryButton, viewInstance.galleryMenu);
        private void OnProfileButtonClicked() => ToggleMenu(null, viewInstance!.profileMenu);

#endregion

#region MVC panels

        private void OnEmotesButtonClicked() => OpenPanelAsync(viewInstance!.EmotesButton, EmotesWheelController.IssueCommand()).Forget();

        private void OnFriendsButtonClicked() =>
            OpenPanelAsync(viewInstance!.FriendsButton, FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.Friends))).Forget();

        private void OnNotificationsButtonClicked() => OpenPanelAsync(viewInstance!.NotificationsButton, NotificationsPanelController.IssueCommand()).Forget();

        private void OnMarketplaceCreditsButtonClicked()
        {
            if (identityCache.IsGuest())
            {
                OpenPanelAsync(viewInstance!.MarketplaceCreditsButton, UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.Credits))).Forget();
                return;
            }

            OpenPanelAsync(viewInstance!.MarketplaceCreditsButton,
                MarketplaceCreditsMenuController.IssueCommand(new MarketplaceCreditsMenuController.Params(isOpenedFromNotification: false))).Forget();
        }

        private void OnNearbyVoiceButtonClicked()
        {
            if (identityCache.IsGuest())
            {
                OpenPanelAsync(null, UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.Voice))).Forget();
                return;
            }

            if (viewInstance!.NearbyVoiceChatButton.IsSuppressed)
            {
                viewInstance.NearbyVoiceChatButton.ShowDisabledTooltip();
                return;
            }

            OpenPanelAsync(null, NearbyVoicePanelController.IssueCommand()).Forget();
        }

        private async UniTaskVoid OpenPanelAsync<TView, TInputData>(
            ISelectableButton? button,
            ShowCommand<TView, TInputData> command)
            where TView: IView
        {
            if (!viewInstance) return;

            CloseOpenMenu();

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

        private void OnMvcManagerViewClosed(IController closedController)
        {
            if (!viewInstance) return;

            //When panels are closed through shortcuts we need to be able to de-select the buttons remotely.
            switch (closedController)
            {
                case EmotesWheelController:
                    viewInstance.EmotesButton.Deselect(); break;
                case FriendsPanelController:
                    viewInstance.FriendsButton.Deselect(); break;
            }
        }

        private void OnMvcManagerViewShowed(IController showedController)
        {
            if (!viewInstance) return;

            // Panels that are controllers and can be opened using shortcuts, we need to select their buttons remotely.
            switch (showedController)
            {
                case EmotesWheelController:
                    viewInstance.EmotesButton.Select();
                    break;
                case FriendsPanelController:
                    viewInstance.FriendsButton.Select();
                    break;
            }
        }

#endregion

        private void OnChatStateChanged(ChatEvents.ChatStateChangedEvent eventData) =>
            OnChatViewFoldingChanged(eventData.CurrentState is not HiddenChatState && eventData.CurrentState is not MinimizedChatState);

        private void OnChatViewFoldingChanged(bool isUnfolded)
        {
            if (isUnfolded)
                viewInstance?.UnreadMessagesButton.Select();
            else
                viewInstance?.UnreadMessagesButton.Deselect();
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
                    if (!(await selfProfile.ProfileAsync(ct)).IsOk(out Profile? myProfile)) return;

                    urlBuilder.Clear();

                    URLAddress url = urlBuilder.AppendDomain(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Host)))
                                               .AppendPath(URLPath.FromString($"profile/accounts/{myProfile.UserId}/referral"))
                                               .Build();

                    webBrowser.OpenUrlMainThreadOnly(url);
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
            }
        }

        private async UniTaskVoid CheckForMarketplaceCreditsFeatureAsync(CancellationToken ct)
        {
            viewInstance?.MarketplaceCreditsButton.gameObject.SetActive(false);

            await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
            if (!(await selfProfile.ProfileAsync(ct)).IsOk(out Profile? ownProfile))
                return;

            isMarketplaceCreditsFeatureEnabled = MarketplaceCreditsUtils.IsUserAllowedToUseTheFeatureAsync(ownProfile.UserId.Value, ct);
            viewInstance?.MarketplaceCreditsButton.gameObject.SetActive(isMarketplaceCreditsFeatureEnabled);

            if (isMarketplaceCreditsFeatureEnabled)
                viewInstance?.MarketplaceCreditsButton.onClick.AddListener(OnMarketplaceCreditsButtonClicked);
        }
    }
}
