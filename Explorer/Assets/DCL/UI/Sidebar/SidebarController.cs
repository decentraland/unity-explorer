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
using DCL.EventsApi;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.InWorldCamera;
using DCL.Friends.UI.FriendPanel;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Prefs;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Buttons;
using DCL.UI.Controls;
using DCL.UI.Sidebar.SubMenus;
using DCL.UI.Skybox;
using DCL.Utilities;
using DCL.UI.ProfileElements;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.VoiceChat.UI;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly URLBuilder urlBuilder = new ();
        private readonly IDisposable chatEventBusSubscription;
        private readonly bool isFriendsFeatureEnabled;
        private readonly bool isNearbyVoiceChatEnabled;
        private readonly bool isDiscoverFeatureEnabled;
        private readonly bool isBugReportFeatureEnabled;
        private readonly SupportRequestService supportRequestService;
        private readonly SmartWearablesSideBarTooltipController smartWearablesTooltipController;
        private readonly HttpEventsApiService eventsApiService;
        private readonly SidebarProfileMenuPresenter profileMenuPresenter;
        private readonly InWorldCameraToggle cameraToggle;
        private readonly AudioMixer generalAudioMixer;
        private readonly VolumeBus volumeBus;
        private readonly List<AudioSliderBinder> audioSliderBinders = new ();
        private readonly bool isCameraReelFeatureEnabled;

        private readonly CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource checkForCommunitiesFeatureCts = new ();
        private CancellationTokenSource referralNotificationCts = new ();
        private CancellationTokenSource openPanelCts = new ();
        private CancellationTokenSource checkForLiveEventsCts = new ();
        private CancellationTokenSource smartWearablesTooltipCts = new ();
        private ReactivePropertyExtensions.DisposableSubscription<bool>? smartWearablesSubscription;

        private Type? openSubMenu;
        private MenuElementView? highlightedOption;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? PlacesOpened;
        public event Action? EventsOpened;

        public SidebarController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            SidebarProfileButtonPresenter profileButtonPresenter,
            UnityAppWebBrowser webBrowser,
            SelfProfile selfProfile,
            IDecentralandUrlsSource decentralandUrlsSource,
            ChatEventBus chatEventBus,
            SupportRequestService supportRequestService,
            SmartWearablesSideBarTooltipController smartWearablesTooltipController,
            HttpEventsApiService eventsApiService,
            SidebarProfileMenuPresenter profileMenuPresenter,
            InWorldCameraToggle cameraToggle,
            AudioMixer generalAudioMixer,
            VolumeBus volumeBus,
            IWeb3IdentityCache identityCache)
            : base(viewFactory)
        {
            this.supportRequestService = supportRequestService;
            this.smartWearablesTooltipController = smartWearablesTooltipController;
            this.eventsApiService = eventsApiService;
            this.profileMenuPresenter = profileMenuPresenter;
            this.cameraToggle = cameraToggle;
            this.generalAudioMixer = generalAudioMixer;
            this.volumeBus = volumeBus;
            this.mvcManager = mvcManager;
            this.identityCache = identityCache;
            this.profileButtonPresenter = profileButtonPresenter;
            this.webBrowser = webBrowser;
            this.selfProfile = selfProfile;
            this.decentralandUrlsSource = decentralandUrlsSource;
            isFriendsFeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.Friends);
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
                viewInstance.SmartWearablesButton.OnButtonHover -= OnSmartWearablesButtonHover;
                viewInstance.SmartWearablesButton.OnButtonUnhover -= OnSmartWearablesButtonUnhover;
                viewInstance.sidebarConfigButton.onClick.RemoveListener(OnSidebarConfigButtonClicked);

                if (isFriendsFeatureEnabled)
                    viewInstance.FriendsButton.onClick.RemoveListener(OnFriendsButtonClicked);

                if (isNearbyVoiceChatEnabled)
                    viewInstance.NearbyVoiceChatButton.Button.onClick.RemoveListener(OnNearbyVoiceButtonClicked);

                UnsubscribeFromOption(viewInstance.discoverPlacesOption, OnPlacesOptionClicked);
                UnsubscribeFromOption(viewInstance.discoverEventsOption, OnEventsOptionClicked);
                UnsubscribeFromOption(viewInstance.discoverCommunitiesOption, OnCommunitiesOptionClicked);
                viewInstance.discoverCloseButton.onClick.RemoveListener(CloseOpenMenu);

                UnsubscribeFromOption(viewInstance.galleryGalleryOption, OnGalleryOptionClicked);
                UnsubscribeFromOption(viewInstance.galleryTakeShotOption, OnTakeShotOptionClicked);
                viewInstance.galleryCloseButton.onClick.RemoveListener(CloseOpenMenu);
                viewInstance.audioCloseButton.onClick.RemoveListener(CloseOpenMenu);

                UnsubscribeFromOption(viewInstance.profileBadgesOption, profileMenuPresenter.OpenBadges);
                UnsubscribeFromOption(viewInstance.profileProfileOption, profileMenuPresenter.OpenProfile);
                UnsubscribeFromOption(viewInstance.profileBackpackOption, profileMenuPresenter.OpenBackpack);
                UnsubscribeFromOption(viewInstance.profileSignOutOption, profileMenuPresenter.SignOut);
                UnsubscribeFromOption(viewInstance.profileExitOption, profileMenuPresenter.Exit);
                viewInstance.profileCloseButton.onClick.RemoveListener(CloseOpenMenu);

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
            referralNotificationCts.SafeCancelAndDispose();
            openPanelCts.SafeCancelAndDispose();
            checkForLiveEventsCts.SafeCancelAndDispose();
            smartWearablesTooltipCts.SafeCancelAndDispose();
            smartWearablesSubscription?.Dispose();
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
            SetMapOpen(true);
            SubscribeToEvents();

            checkForCommunitiesFeatureCts = checkForCommunitiesFeatureCts.SafeRestart();
            CheckForCommunitiesFeatureAsync(checkForCommunitiesFeatureCts.Token).Forget();

            OnChatViewFoldingChanged(true);

            viewInstance.SetLiveEventsCounter(0);

            smartWearablesSubscription = smartWearablesTooltipController.HasRunningSmartWearables.UseCurrentValueAndSubscribeToUpdate(
                this, static (hasAny, controller) => controller.OnRunningSmartWearablesChanged(hasAny), CancellationToken.None);
        }

        protected override void OnViewShow()
        {
            //We load the data into the profile widget
            profileButtonPresenter.LoadProfile();

            if (!isDiscoverFeatureEnabled) return;

            checkForLiveEventsCts = checkForLiveEventsCts.SafeRestart();
            FillLiveEventsAsync(checkForLiveEventsCts.Token).Forget();
        }

        private void OnRunningSmartWearablesChanged(bool hasAny)
        {
            viewInstance!.SmartWearablesButton.gameObject.SetActive(hasAny);

            if (!hasAny)
                smartWearablesTooltipController.Close();
        }

        private void OnSidebarConfigButtonClicked() =>
            OpenPanelAsync(viewInstance!.sidebarConfigButton, SidebarSettingsWidgetController.IssueCommand()).Forget();

        private void OnSmartWearablesButtonHover()
        {
            smartWearablesTooltipCts = smartWearablesTooltipCts.SafeRestart();
            ShowSmartWearablesTooltipAsync(smartWearablesTooltipCts.Token).Forget();
        }

        private void OnSmartWearablesButtonUnhover() =>
            smartWearablesTooltipController.Close();

        private async UniTaskVoid ShowSmartWearablesTooltipAsync(CancellationToken ct)
        {
            try { await mvcManager.ShowAsync(SmartWearablesSideBarTooltipController.IssueCommand(), ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private async UniTaskVoid FillLiveEventsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Result<IReadOnlyList<EventDTO>> liveEventsResult = await eventsApiService.GetEventsAsync(ct, onlyLiveEvents: true)
                                                                                         .SuppressToResultAsync(ReportCategory.EVENTS);

                if (ct.IsCancellationRequested)
                    return;

                viewInstance!.SetLiveEventsCounter(liveEventsResult.Success ? liveEventsResult.Value.Count : 0);
                await UniTask.Delay(TimeSpan.FromMinutes(3), cancellationToken: ct);
            }
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
            viewInstance.SmartWearablesButton.OnButtonHover += OnSmartWearablesButtonHover;
            viewInstance.SmartWearablesButton.OnButtonUnhover += OnSmartWearablesButtonUnhover;
            viewInstance.sidebarConfigButton.onClick.AddListener(OnSidebarConfigButtonClicked);

            SubscribeToOption(viewInstance.discoverPlacesOption, OnPlacesOptionClicked);
            SubscribeToOption(viewInstance.discoverEventsOption, OnEventsOptionClicked);
            SubscribeToOption(viewInstance.discoverCommunitiesOption, OnCommunitiesOptionClicked);
            viewInstance.discoverCloseButton.onClick.AddListener(CloseOpenMenu);

            SubscribeToOption(viewInstance.galleryGalleryOption, OnGalleryOptionClicked);
            SubscribeToOption(viewInstance.galleryTakeShotOption, OnTakeShotOptionClicked);
            viewInstance.galleryCloseButton.onClick.AddListener(CloseOpenMenu);
            viewInstance.audioCloseButton.onClick.AddListener(CloseOpenMenu);

            SubscribeToOption(viewInstance.profileBadgesOption, profileMenuPresenter.OpenBadges);
            SubscribeToOption(viewInstance.profileProfileOption, profileMenuPresenter.OpenProfile);
            SubscribeToOption(viewInstance.profileBackpackOption, profileMenuPresenter.OpenBackpack);
            SubscribeToOption(viewInstance.profileSignOutOption, profileMenuPresenter.SignOut);
            SubscribeToOption(viewInstance.profileExitOption, profileMenuPresenter.Exit);
            viewInstance.profileCloseButton.onClick.AddListener(CloseOpenMenu);

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
        /// <summary>
        ///     Submenus run as MVC popups, so the shared popup closer dismisses them on an outside click.
        /// </summary>
        private void ToggleSubMenu<TView>(SimpleHoverableButton? button) where TView: ViewBase, IView
        {
            bool alreadyOpen = openSubMenu == typeof(TView);

            CloseOpenMenu();

            if (alreadyOpen) return;

            openSubMenu = typeof(TView);

            OpenSubMenuAsync<TView>(button).Forget();
        }

        private async UniTaskVoid OpenSubMenuAsync<TView>(SimpleHoverableButton? button) where TView: ViewBase, IView
        {
            try { await OpenPanelAsync(button, SidebarSubMenuController<TView>.IssueCommand()); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
            finally
            {
                if (openSubMenu == typeof(TView))
                    openSubMenu = null;
            }
        }

        private void CloseOpenMenu()
        {
            if (openSubMenu == null) return;

            openSubMenu = null;
            ClearHighlightedOption();

            openPanelCts = openPanelCts.SafeRestart();
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

        private void OnCommunitiesOptionClicked() =>
            OpenExplorePanelInSection(ExploreSections.Communities);

        private void OnExploreEmotesClicked() =>
            OpenShopCategory(EMOTE_CATEGORY);

        private void OnExploreWearablesClicked() =>
            OpenShopCategory(WEARABLE_CATEGORY);

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

        private void OnGalleryOptionClicked() =>
            OpenExplorePanelInSection(ExploreSections.CameraReel);

        private void OnTakeShotOptionClicked()
        {
            CloseOpenMenu();
            cameraToggle.Toggle(SOURCE_BUTTON);
        }

        private void OnSettingsOptionClicked() =>
            OpenExplorePanelInSection(ExploreSections.Settings);

        private void OnControlsOptionClicked() =>
            OpenPanelAsync(null, ControlsPanelController.IssueCommand()).Forget();

        private void OnReportBugOptionClicked() =>
            OpenPanelAsync(null, BugReportController.IssueCommand(new BugReportParams())).Forget();

        private void OnContactSupportOptionClicked()
        {
            CloseOpenMenu();
            supportRequestService.OpenSupport();
        }

        private void OnDiscordOptionClicked() =>
            OpenWebUrl(DecentralandUrl.Discord);

        private void OnFaqOptionClicked() =>
            OpenWebUrl(DecentralandUrl.Faqs);

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
            viewInstance!.DiscoverButton.SetSelected(false);
            viewInstance.ShopButton.SetSelected(false);
            viewInstance.SettingsButton.SetSelected(false);
            viewInstance.AudioSettingsButton.SetSelected(false);
            viewInstance.GalleryButton.SetSelected(false);
            viewInstance.NotificationsButton.SetSelected(false);
            viewInstance.FriendsButton.SetSelected(false);
            viewInstance.EmotesButton.SetSelected(false);

            ClearHighlightedOption();

            openSubMenu = null;
        }

        /// <summary>
        ///     The minimap stays usable alongside any other menu, so it is toggled outside the single-menu group.
        /// </summary>
        private void OnMapButtonClicked()
        {
            SetMapOpen(!viewInstance!.MapButton.IsSelected);
        }

        private void SetMapOpen(bool open)
        {
            viewInstance!.MapButton.SetSelected(open);
            viewInstance.minimapMenu.SetActive(open);
        }

        private void OnDiscoverButtonClicked() =>
            ToggleSubMenu<DiscoverMenuView>(viewInstance!.DiscoverButton);

        private void OnShopButtonClicked() =>
            ToggleSubMenu<ShopMenuView>(viewInstance!.ShopButton);

        private void OnSettingsButtonClicked() =>
            ToggleSubMenu<SystemMenuView>(viewInstance!.SettingsButton);

        private void OnAudioSettingsButtonClicked()
        {
            RefreshAudioSliders();
            ToggleSubMenu<AudioMenuView>(viewInstance!.AudioSettingsButton);
        }

        private void OnGalleryButtonClicked() =>
            ToggleSubMenu<GalleryMenuView>(viewInstance!.GalleryButton);

        private void OnProfileButtonClicked()
        {
            profileMenuPresenter.Refresh();
            ToggleSubMenu<SidebarProfileMenuView>(null);
        }
#endregion

#region MVC panels
        private void OnEmotesButtonClicked() =>
            OpenPanelAsync(viewInstance!.EmotesButton, EmotesWheelController.IssueCommand()).Forget();

        private void OnFriendsButtonClicked() =>
            OpenPanelAsync(viewInstance!.FriendsButton, FriendsPanelController.IssueCommand(new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.Friends))).Forget();

        private void OnNotificationsButtonClicked() =>
            OpenPanelAsync(viewInstance!.NotificationsButton, NotificationsPanelController.IssueCommand()).Forget();

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

        private async UniTask OpenPanelAsync<TView, TInputData>(
            ISelectableButton? button,
            ShowCommand<TView, TInputData> command)
            where TView: IView
        {
            if (!viewInstance) return;

            ClearHighlightedOption();

            openPanelCts = openPanelCts.SafeRestart();
            CancellationToken ct = openPanelCts.Token;

            viewInstance.BlockSidebar();
            button?.Select();

            try { await mvcManager.ShowAsync(command, ct); }
            catch (OperationCanceledException) { } // Expected cancellation when a new panel is opened
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
    }
}
