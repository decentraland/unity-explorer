using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.CharacterPreview;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.Events;
using DCL.EventsApi;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Utility;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world loads and on demand in-world. It only reports the close intent.
    /// </summary>
    public class LobbyDocumentController : ControllerBase<LobbyDocumentView, LobbyParameter>
    {
        private const string LIVE_EVENT_HOST_FORMAT = "By {0}";
        private const string WELCOME_FALLBACK = "Welcome!";
        private const string WELCOME_FORMAT = "Welcome {0}!";

        /// <summary>What a world named without its domain gets, as in the /goto chat command.</summary>
        private const string WORLD_SUFFIX = ".dcl.eth";
        private const string WORLD_ABOUT_PATH = "/about";
        private const string CROWD_LOOKUP_ERROR_MESSAGE = "The most crowded place could not be found. Please try again.";
        private const int MAX_UPCOMING_EVENTS = 10;

        private const int MAX_RECENT_PLACES = 3;

        /// <summary>Recent places the four-column section has room for.</summary>
        private const int MAX_RECENT_PLACES_4_BY_2 = 2;

        /// <summary>How many of the user's communities the rails page through.</summary>
        private const int MAX_COMMUNITIES = 10;

        /// <summary>How many of the user's latest photos the rails page through.</summary>
        private const int MAX_PHOTOS = 10;

        private static readonly Comparison<EventDTO> BY_START_TIME = static (a, b) => a.NextStartAtProcessed.CompareTo(b.NextStartAtProcessed);

        /// <summary>Panel pixels from the pointer to the left edge of the avatar tooltip.</summary>
        private static readonly Vector2 AVATAR_TOOLTIP_OFFSET = new (100f, 0f);

        private readonly IInputBlock inputBlock;
        private readonly ICursor cursor;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly SelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly LobbyAvatarSettings avatarSettings;
        private readonly LobbyStage stage;
        private readonly World world;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IHomePlaceSource homePlace;
        private readonly HttpEventsApiService eventsApiService;
        private readonly EventCardActionsController eventCardActions;
        private readonly IRealmNavigator realmNavigator;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebRequestController webRequestController;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly StartParcel startParcel;
        private readonly ISpriteCache spriteCache;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly LobbyProfileMenuPresenter profileMenu;
        private readonly NotificationsPanelController<LobbyPopupParameter> notificationsPanel;
        private readonly List<PlacesData.PlaceInfo> recentPlaces = new ();
        private readonly List<PlacesData.PlaceInfo> featuredPlaces = new ();
        private readonly List<EventDTO> liveEvents = new ();
        private readonly List<EventDTO> upcomingEvents = new ();
        private readonly List<string> eventPlaceIds = new ();
        private readonly Dictionary<string, PlacesData.PlaceInfo> eventPlaces = new ();
        private readonly List<GetUserCommunitiesData.CommunityData> communities = new ();
        private readonly List<CameraReelResponseCompact> photos = new ();
        private readonly LobbyDocumentFriendsPresenter? friends;
        private readonly LobbyConnectedFriendsPresenter? connectedFriends;

        // Null while the communities feature is off
        private readonly CommunitiesDataProvider? communitiesProvider;

        // Null while the camera reel feature is off
        private readonly CameraReelContainer? cameraReel;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ClickOrDragManipulator avatarGesture;
        private readonly LobbyWidgetHost widgetHost = new ();
        private readonly LobbyLayoutEditor layoutEditor;
        private readonly bool includeDiscover;

        private LobbyPlacesRail? recentPlacesRail;
        private LobbyPlacesRail? recentPlaces4By2Rail;
        private LobbyPlacesRail? featuredPlacesRail;
        private LobbyPlacesRail? featuredPlaces4By2Rail;
        private LobbyLiveEventsRail? liveEventsRail;
        private LobbyUpcomingEventsRail? upcomingEventsRail;
        private LobbyCommunitiesRail? communitiesRail;
        private LobbyCommunitiesRail? communities4By2Rail;
        private LobbyGalleryRail? galleryRail;
        private LobbyGalleryRail? gallery4By2Rail;
        private LobbyCharacterPreviewController? avatarPreview;

        // Null while the landing place loads
        private PlacesData.PlaceInfo? landingPlace;

        // Kept out of the place: the lookups return the cached instance other panels read
        private string[]? landingConnectedAddresses;

        // The destination the shown landing place describes; null together with the place
        private LandingDestination? landingDestination;
        private LandingDestination? startupDestination;

        // One menu serves every Share button; the event it acts on is the one whose button opened it
        private GenericContextMenu? shareMenu;
        private EventDTO sharedEvent;

        // Without its place the details show a world event at the raw parcel, 0,0, so they wait for the places request
        private UniTask eventPlacesResolved = UniTask.CompletedTask;

        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
        private CancellationTokenSource? eventsCts;
        private CancellationTokenSource? friendsCts;
        private CancellationTokenSource? communitiesCts;
        private CancellationTokenSource? galleryCts;
        private CancellationTokenSource? deletePhotoCts;
        private CancellationTokenSource? jumpInCts;
        private CancellationTokenSource? goToCts;
        private CancellationTokenSource? profileMenuCts;
        private CancellationTokenSource? notificationsCts;
        private UniTaskCompletionSource? closeIntent;
        private bool leaving;
        private bool communitiesSectionsShown;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // At startup the lobby is the only way into the world, so it cannot be dismissed before a destination is picked;
        // while the layout is edited Escape leaves edit mode instead, which the editor handles itself
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed && !layoutEditor.IsEditing;

        /// <summary>The panel is on screen; true at the startup show, false when opened from the world.</summary>
        public event Action<bool>? Opened;

        /// <summary>The panel left the screen, once per <see cref="Opened" />.</summary>
        public event Action? Closed;

        /// <summary>A place card was picked, which opens its details.</summary>
        public event Action<PlacesData.PlaceInfo, LobbyCardOrigin>? PlaceOpened;

        /// <summary>The user is on their way to a place, from a card or from the details it opened.</summary>
        public event Action<PlacesData.PlaceInfo, LobbyCardOrigin>? PlaceJumpedIn;

        /// <summary>An event card was picked, which opens its details.</summary>
        public event Action<IEventDTO, LobbyCardOrigin>? EventOpened;

        /// <summary>The user is on their way to an event, from the details its card opened.</summary>
        public event Action<IEventDTO, LobbyCardOrigin>? EventJumpedIn;

        /// <summary>The user is on their way to a friend, with the parcel the friend was at.</summary>
        public event Action<string, Vector2Int>? FriendJoined;

        /// <summary>A top bar button opened an explore section over the panel.</summary>
        public event Action<ExploreSections>? ExploreSectionOpened;

        public LobbyDocumentController(ViewFactoryMethod viewFactory,
            IInputBlock inputBlock,
            ICursor cursor,
            IReadOnlyLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            SelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            LobbyAvatarSettings avatarSettings,
            LobbyStage stage,
            World world,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IHomePlaceSource homePlace,
            HttpEventsApiService eventsApiService,
            EventCardActionsController eventCardActions,
            IRealmNavigator realmNavigator,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWebRequestController webRequestController,
            IChatMessagesBus chatMessagesBus,
            StartParcel startParcel,
            ISpriteCache spriteCache,
            SidebarProfileButtonPresenter profileButtonPresenter,
            LobbyProfileMenuPresenter profileMenu,
            NotificationsPanelController<LobbyPopupParameter> notificationsPanel,
            LobbyDocumentFriendsPresenter? friends,
            LobbyConnectedFriendsPresenter? connectedFriends,
            CommunitiesDataProvider? communitiesProvider,
            CameraReelContainer? cameraReel,
            IWeb3IdentityCache identityCache) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.cursor = cursor;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.avatarSettings = avatarSettings;
            this.stage = stage;
            this.world = world;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.homePlace = homePlace;
            this.eventsApiService = eventsApiService;
            this.eventCardActions = eventCardActions;
            this.realmNavigator = realmNavigator;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webRequestController = webRequestController;
            this.chatMessagesBus = chatMessagesBus;
            this.startParcel = startParcel;
            this.spriteCache = spriteCache;
            this.profileButtonPresenter = profileButtonPresenter;
            this.profileMenu = profileMenu;
            this.notificationsPanel = notificationsPanel;
            this.friends = friends;
            this.connectedFriends = connectedFriends;
            this.communitiesProvider = communitiesProvider;
            this.cameraReel = cameraReel;
            this.identityCache = identityCache;
            includeDiscover = FeaturesRegistry.Instance.IsEnabled(FeatureId.Discover);

            avatarGesture = new ClickOrDragManipulator
            {
                Clicked = OnAvatarClicked,
                DragStarted = OnAvatarDragStarted,
                Dragged = OnAvatarDragged,
                DragEnded = OnAvatarDragEnded,
            };

            layoutEditor = new LobbyLayoutEditor(widgetHost, cursor);

            if (friends != null)
                friends.JoinRequested = OnFriendJoin;

            profileMenu.SettingsRequested = ShowSettings;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
                viewInstance.Profile.Clicked = null;

            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;
            startParcel.JumpInRequestRaised -= RequestClose;
            eventCardActions.EventSetAsInterested -= OnEventInterestChanged;
            UnsubscribeFromCommunityChanges();
            UnsubscribeFromReelDeletions();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            notificationsPanel.UnreadCountChanged -= ShowUnreadCount;
            DCLInput.Instance.UI.Close.performed -= OnUIClose;
            profileMenu.SettingsRequested = null;
            profileMenu.OpenChanged = null;

            if (friends != null)
                friends.JoinRequested = null;

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
            friendsCts.SafeCancelAndDispose();
            communitiesCts.SafeCancelAndDispose();
            galleryCts.SafeCancelAndDispose();
            deletePhotoCts.SafeCancelAndDispose();
            jumpInCts.SafeCancelAndDispose();
            goToCts.SafeCancelAndDispose();
            profileMenuCts.SafeCancelAndDispose();
            notificationsCts.SafeCancelAndDispose();
            avatarPreview?.Dispose();
            closeIntent?.TrySetCanceled();
        }

        // The uGUI preview is nested in the prefab, so unlike the hierarchy it exists from the instantiation on
        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            avatarPreview = new LobbyCharacterPreviewController(viewInstance!.CharacterPreviewView, avatarSettings, stage, characterPreviewFactory, world, characterPreviewEventBus);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;
            leaving = false;
        }

        // The hierarchy exists from the first show on, so the rows get their section here, not at instantiation
        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            // The renderer keeps the hierarchy across hides, so every handler is removed before it is added again
            viewInstance!.AttachTopBarWidgets();
            widgetHost.Show(viewInstance.Root);
            viewInstance.Profile.Clicked = profileMenu.Toggle;
            profileMenu.OpenChanged = SetProfileMenuOpen;
            viewInstance.NotificationsButton.clicked -= ShowNotifications;
            viewInstance.NotificationsButton.clicked += ShowNotifications;
            viewInstance.MapButton.clicked -= ShowMap;
            viewInstance.MapButton.clicked += ShowMap;
            viewInstance.PlacesButton.clicked -= ShowPlaces;
            viewInstance.PlacesButton.clicked += ShowPlaces;
            viewInstance.PlacesButton.SetDisplayed(includeDiscover);
            viewInstance.EventsButton.clicked -= ShowEvents;
            viewInstance.EventsButton.clicked += ShowEvents;
            viewInstance.EventsButton.SetDisplayed(includeDiscover);
            viewInstance.BackButton.clicked -= RequestClose;
            viewInstance.BackButton.clicked += RequestClose;
            viewInstance.SetBackButtonDisplayed(!inputData.IsStartup);
            viewInstance.EditLayoutButton.clicked -= ToggleLayoutEditing;
            viewInstance.EditLayoutButton.clicked += ToggleLayoutEditing;
            profileButtonPresenter.LoadProfile();

            profileMenuCts = profileMenuCts.SafeRestart();
            profileMenu.Show(viewInstance.ProfileMenu, profileMenuCts.Token);

            // The UI map stays disabled until the first world loads, so the one action the popups need is enabled on its own.
            if (inputData.IsStartup)
                DCLInput.Instance.UI.Close.Enable();

            DCLInput.Instance.UI.Close.performed -= OnUIClose;
            DCLInput.Instance.UI.Close.performed += OnUIClose;

            notificationsPanel.UnreadCountChanged += ShowUnreadCount;
            ShowUnreadCount(notificationsPanel.UnreadCount);

            // Hidden until the avatar is up again, so the empty stage takes no clicks
            VisualElement avatarHitArea = viewInstance.AvatarHitArea;
            avatarHitArea.SetDisplayed(false);
            avatarHitArea.AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);
            avatarHitArea.AddManipulator(avatarGesture);
            VisualElement avatarTooltip = viewInstance.AvatarTooltip;
            avatarTooltip.SetDisplayed(false);
            avatarHitArea.RegisterCallback<PointerEnterEvent, VisualElement>(OnAvatarPointerEnter, avatarTooltip);
            avatarHitArea.RegisterCallback<PointerMoveEvent, VisualElement>(OnAvatarPointerMove, avatarTooltip);
            avatarHitArea.RegisterCallback<PointerLeaveEvent, VisualElement>(OnAvatarPointerLeave, avatarTooltip);

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
            avatarCts = avatarCts.SafeRestart();
            ShowAvatarAsync(avatarCts.Token).Forget();

            LobbyLandingCardElement landingCard = viewInstance.LandingCard;
            landingCard.Clicked = OnLandingCardClicked;
            landingCard.JumpInClicked = OnLandingJumpInClicked;
            viewInstance.GoToField.Submitted = OnGoToSubmitted;

            // The four-column sections list the same places as the seven-column ones, so their rails share the lists
            recentPlacesRail ??= CreatePlacesRail(recentPlaces, LobbyCardOrigin.Recent, PlacesSection.RecentlyVisited);
            recentPlaces4By2Rail ??= CreatePlacesRail(recentPlaces, LobbyCardOrigin.Recent, PlacesSection.RecentlyVisited);
            featuredPlacesRail ??= CreatePlacesRail(featuredPlaces, LobbyCardOrigin.Recommended, PlacesSection.Browse);
            featuredPlaces4By2Rail ??= CreatePlacesRail(featuredPlaces, LobbyCardOrigin.Recommended, PlacesSection.Browse);
            recentPlacesRail.Show(viewInstance.RecentPlaces);
            recentPlaces4By2Rail.Show(viewInstance.RecentPlaces4By2);
            featuredPlacesRail.Show(viewInstance.FeaturedPlaces);
            featuredPlaces4By2Rail.Show(viewInstance.FeaturedPlaces4By2);

            liveEventsRail ??= new LobbyLiveEventsRail(viewInstance.LiveEventCardTemplate) { CardClicked = OnLiveEventClicked, CardJumpInClicked = OnLiveEventJumpInClicked, ViewAllClicked = ShowEvents };
            upcomingEventsRail ??= CreateUpcomingEventsRail();
            liveEventsRail.Show(viewInstance.LiveEvents);
            upcomingEventsRail.Show(viewInstance.UpcomingEvents);

            // The four-column rail lists the same communities as the seven-column one; only a user the feature admits gets
            // the sections, which a verdict still pending here brings up from the fetch
            communitiesRail ??= new LobbyCommunitiesRail(viewInstance.CommunityCardTemplate) { CardClicked = OnCommunityClicked, ViewAllClicked = ShowCommunities };
            communities4By2Rail ??= new LobbyCommunitiesRail(viewInstance.CommunityCardTemplate) { CardClicked = OnCommunityClicked, ViewAllClicked = ShowCommunities };

            if (communitiesProvider != null && CommunitiesFeatureAccess.Instance.IsUserAllowedCached())
                ShowCommunitiesSections();

            // The four-column rail lists the same photos as the seven-column one
            galleryRail ??= new LobbyGalleryRail(viewInstance.GalleryCardTemplate) { CardClicked = OnPhotoClicked, ViewAllClicked = ShowGallery };
            gallery4By2Rail ??= new LobbyGalleryRail(viewInstance.GalleryCardTemplate) { CardClicked = OnPhotoClicked, ViewAllClicked = ShowGallery };

            if (cameraReel != null && identityCache.Identity != null)
            {
                galleryRail.Show(viewInstance.Gallery);
                gallery4By2Rail.Show(viewInstance.Gallery4By2);
            }

            // Before the places: a cached landing place binds its card synchronously
            friendsCts = friendsCts.SafeRestart();
            friends?.Show(viewInstance.Friends, friendsCts.Token);
            connectedFriends?.Show(viewInstance.FriendTooltip, friendsCts.Token);

            placesCts = placesCts.SafeRestart();
            LandingDestination destination = ResolveLandingDestination();

            // A card already showing the destination keeps it up during the refetch
            if (landingDestination is not { } shown || !shown.Equals(destination))
                ShowLandingCardLoading(landingCard);

            ShowLandingPlaceAsync(destination, placesCts.Token).Forget();
            ShowPlacesAsync(recentPlacesRail, recentPlaces4By2Rail, recentPlaces, placesAPIService.GetRecentlyVisitedDestinationsAsync(placesCts.Token, withConnectedUsers: true), MAX_RECENT_PLACES, MAX_RECENT_PLACES_4_BY_2, placesCts.Token).Forget();
            ShowPlacesAsync(featuredPlacesRail, featuredPlaces4By2Rail, featuredPlaces, placesAPIService.GetHighlightedDestinationsAsync(placesCts.Token, withConnectedUsers: true), int.MaxValue, int.MaxValue, placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            eventCardActions.EventSetAsInterested += OnEventInterestChanged;
            ShowEventsAsync(eventsCts.Token).Forget();

            // Every show fetches again: a community joined or left while the lobby was down is reflected on the way back;
            // one changed from a panel over the lobby refetches through the provider's events
            communitiesCts = communitiesCts.SafeRestart();
            ShowCommunitiesAsync(communitiesCts.Token).Forget();
            SubscribeToCommunityChanges();

            // Every show fetches again: a photo taken since the lobby was last up is reflected on the way back; one
            // deleted from a panel over the lobby drops out through the gallery's events
            galleryCts = galleryCts.SafeRestart();
            ShowGalleryAsync(galleryCts.Token).Forget();
            SubscribeToReelDeletions();

            if (inputData.IsStartup)
                startParcel.JumpInRequestRaised += RequestClose;

            Opened?.Invoke(inputData.IsStartup);
        }

        protected override void OnViewClose()
        {
            if (inputData.IsStartup)
                startParcel.JumpInRequestRaised -= RequestClose;

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            eventCardActions.EventSetAsInterested -= OnEventInterestChanged;
            UnsubscribeFromCommunityChanges();
            UnsubscribeFromReelDeletions();
            notificationsPanel.UnreadCountChanged -= ShowUnreadCount;
            DCLInput.Instance.UI.Close.performed -= OnUIClose;

            // Undoes the startup-only enable, which bypassed the still disabled UI map.
            if (inputData.IsStartup)
                DCLInput.Instance.UI.Close.Disable();

            profileMenu.Hide();
            profileMenuCts.SafeCancelAndDispose();
            profileMenuCts = null;
            notificationsCts.SafeCancelAndDispose();
            notificationsCts = null;

            viewInstance?.GoToField.Reset();

            // The next show starts outside edit mode, like the first one
            layoutEditor.Hide();

            // Nulled so a late callback finds no source rather than a disposed one, whose token throws
            avatarCts.SafeCancelAndDispose();
            avatarCts = null;
            placesCts.SafeCancelAndDispose();
            placesCts = null;
            goToCts.SafeCancelAndDispose();
            goToCts = null;
            eventsCts.SafeCancelAndDispose();
            eventsCts = null;
            friends?.Hide();
            connectedFriends?.Hide();
            friendsCts.SafeCancelAndDispose();
            friendsCts = null;
            communitiesCts.SafeCancelAndDispose();
            communitiesCts = null;
            galleryCts.SafeCancelAndDispose();
            galleryCts = null;

            // A drag cut short by the close leaves the hardware cursor hidden otherwise
            avatarGesture.Cancel();
            avatarPreview!.OnHide();

            recentPlacesRail?.Hide();
            recentPlaces4By2Rail?.Hide();
            featuredPlacesRail?.Hide();
            featuredPlaces4By2Rail?.Hide();
            liveEventsRail?.Hide();
            upcomingEventsRail?.Hide();
            communitiesRail?.Hide();
            communities4By2Rail?.Hide();
            communitiesSectionsShown = false;
            galleryRail?.Hide();
            gallery4By2Rail?.Hide();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

            // At startup a panel opened from the lobby replaces it; without coming back the flow never resumes
            if (inputData.IsStartup && !leaving)
                mvcManager.OnViewClosed += ShowAgainWhenTheScreenIsFree;

            Closed?.Invoke();
        }

        // Shows the startup lobby again once the replacing panel is gone; a Logout cancels the startup token instead
        private void ShowAgainWhenTheScreenIsFree(IController closed)
        {
            // This handler is added while the lobby is hiding, so its own closure is reported to it first
            if (closed == this || mvcManager.IsAnyModalViewShowing()) return;

            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;

            if (inputData.StartupToken.IsCancellationRequested) return;

            // A destination requested while the lobby was covered releases the flow without showing it again
            if (startParcel.JumpInRequested)
                RequestClose();
            else
                mvcManager.ShowAndForget(IssueCommand(inputData));
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            var intent = new UniTaskCompletionSource();
            closeIntent = intent;

            // A request made while the view was loading or covered by another panel had no listener
            if (inputData.IsStartup && startParcel.JumpInRequested)
                RequestClose();

            await intent.Task.AttachExternalCancellation(ct);
        }

        private async UniTaskVoid ShowAvatarAsync(CancellationToken ct)
        {
            try
            {
                ProfileReadResult profileResult = await selfProfile.ProfileAsync(ct);

                if (ct.IsCancellationRequested) return;

                if (!profileResult.IsOk(out Profile? profile))
                {
                    ShowWelcome(null);
                    ReportHub.LogWarning(ReportCategory.PROFILE, "Own profile is not available, the lobby avatar is not shown");
                    return;
                }

                ShowWelcome(profile);

                avatarPreview!.Initialize(profile.Avatar, CharacterPreviewUtils.LOBBY_PREVIEW_POSITION);
                avatarPreview.OnBeforeShow();
                avatarPreview.OnShow();

                viewInstance!.AvatarHitArea.SetDisplayed(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        private void ToggleLayoutEditing()
        {
            if (layoutEditor.IsEditing)
                layoutEditor.Hide();
            else
            {
                goToCts.SafeCancelAndDispose();
                goToCts = null;
                viewInstance!.GoToField.Reset();
                layoutEditor.Show(viewInstance.Root);
            }
        }

        private void OnProfileUpdated(Profile profile)
        {
            ShowWelcome(profile);
            avatarPreview!.Refresh(profile.Avatar);
        }

        private void ShowWelcome(Profile? profile)
        {
            string? name = profile?.ValidatedName;
            viewInstance!.WelcomeText.text = string.IsNullOrEmpty(name) ? WELCOME_FALLBACK : string.Format(WELCOME_FORMAT, name);
        }

        private void SetProfileMenuOpen(bool open) =>
            viewInstance!.Profile.IsMenuOpen = open;

        private void ShowUnreadCount(int count)
        {
            Label badge = viewInstance!.UnreadBadge;
            badge.text = count.ToString();
            badge.SetDisplayed(count > 0);
        }

        private void ShowLandingCardLoading(LobbyLandingCardElement card)
        {
            landingPlace = null;
            landingDestination = null;
            landingConnectedAddresses = null;
            card.Title = string.Empty;
            card.Creator = string.Empty;
            card.OnlineCount = 0;
            card.CanJumpIn = false;
            card.CanOpen = false;
            card.Thumbnail = null;
            card.IsLoading = true;
            BindConnectedFriends(card.ConnectedFriends, null);
        }

        // At startup the hero card is the only way out, so an offline stand-in fills it when the Places API fails
        private async UniTaskVoid ShowLandingPlaceAsync(LandingDestination destination, CancellationToken ct)
        {
            Result<PlacesData.PlaceInfo?> result = await (destination.WorldName != null
                    ? placesAPIService.GetWorldByNameAsync(destination.WorldName, ct)
                    : placesAPIService.GetPlaceAsync(destination.Parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            // A hide mid-load left the kept card on its loading look, so its picture is loaded again
            if (!result.Success && landingDestination is { } shown && shown.Equals(destination) && landingPlace is { } kept)
            {
                LoadThumbnailAsync(viewInstance!.LandingCard, kept.image, ReportCategory.PLACES, ct).Forget();
                return;
            }

            PlacesData.PlaceInfo? place = result.Success ? result.Value : null;
            ShowLandingCard(destination, place ?? destination.ToOfflinePlace(), hasDetails: place != null, ct);

            if (place != null)
                RefreshLandingConnectedUsersAsync(place, ct).Forget();
        }

        // The coords and name lookups never carry the connected users, so they are fetched again by id
        private async UniTaskVoid RefreshLandingConnectedUsersAsync(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetDestinationsByIdsAsync(new[] { place.id }, ct, withConnectedUsers: true)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested || !result.Success || result.Value.Data.Count == 0 || !ReferenceEquals(landingPlace, place)) return;

            landingConnectedAddresses = result.Value.Data[0].connected_addresses;

            LobbyLandingCardElement card = viewInstance!.LandingCard;
            card.OnlineCount = landingConnectedAddresses?.Length ?? place.user_count;
            BindConnectedFriends(card.ConnectedFriends, landingConnectedAddresses);
        }

        // The launch pick is frozen on the first show; only a home destination keeps following the movable home
        private LandingDestination ResolveLandingDestination()
        {
            startupDestination ??= new LandingDestination(startParcel.Peek(), realmData.IsWorld() ? realmData.RealmName : null);

            if (startParcel.Source != StartParcelSource.Home)
                return startupDestination.Value;

            if (homePlace.IsWorldHome && homePlace.CurrentHomeWorldName is { } homeWorld)
                return new LandingDestination(Vector2Int.zero, homeWorld);

            if (homePlace.CurrentHomeCoordinates is { } homeParcel)
                return new LandingDestination(homeParcel, null);

            // Home was unset during the session
            return startupDestination.Value;
        }

        private void ShowLandingCard(LandingDestination destination, PlacesData.PlaceInfo place, bool hasDetails, CancellationToken ct)
        {
            // A refetch of the shown place keeps its connected users until they are resolved again
            if (landingPlace?.id != place.id)
                landingConnectedAddresses = null;

            landingPlace = place;
            landingDestination = destination;

            LobbyLandingCardElement card = viewInstance!.LandingCard;
            card.CanJumpIn = true;
            card.CanOpen = hasDetails;
            ShowPlaceCard(card, place, landingConnectedAddresses ?? place.connected_addresses, ct);
        }

        private void OnLandingCardClicked()
        {
            if (landingPlace != null)
                OnPlaceClicked(landingPlace, LobbyCardOrigin.Landing);
        }

        private void OnLandingJumpInClicked()
        {
            if (landingPlace == null) return;

            // Before the world loads the card mirrors the destination already picked, so there is nothing to reassign
            if (!startParcel.IsConsumed())
            {
                PlaceJumpedIn?.Invoke(landingPlace, LobbyCardOrigin.Landing);
                RequestClose();
            }
            // A home is the exact parcel the user stood on, not a place
            else if (startParcel.Source == StartParcelSource.Home && landingDestination is { WorldName: null } home)
            {
                PlaceJumpedIn?.Invoke(landingPlace, LobbyCardOrigin.Landing);
                PickParcel(home.Parcel, landOnParcel: true);
            }
            else
                OnPlaceJumpIn(landingPlace, LobbyCardOrigin.Landing);
        }

        private void OnAvatarClicked() =>
            mvcManager.ShowAndForget(BackpackModalController.IssueCommand(new BackpackModalParameter(BackpackSections.Avatar)));

        private void OnAvatarPointerEnter(PointerEnterEvent evt, VisualElement tooltip)
        {
            avatarPreview!.SetHovered(true);
            tooltip.MoveToPointer(evt.position, AVATAR_TOOLTIP_OFFSET);
            tooltip.SetDisplayed(true);
        }

        private static void OnAvatarPointerMove(PointerMoveEvent evt, VisualElement tooltip) =>
            tooltip.MoveToPointer(evt.position, AVATAR_TOOLTIP_OFFSET);

        private void OnAvatarPointerLeave(PointerLeaveEvent evt, VisualElement tooltip)
        {
            avatarPreview!.SetHovered(false);
            tooltip.SetDisplayed(false);
        }

        private void OnAvatarDragStarted(Vector2 panelPosition)
        {
            viewInstance!.AvatarTooltip.SetDisplayed(false);

            VisualElement dragCursor = viewInstance.AvatarDragCursor;
            dragCursor.MoveToPointer(panelPosition, Vector2.zero);
            dragCursor.SetDisplayed(true);
            cursor.SetVisibility(false);
        }

        // The figure turns by screen pixels per second as in the other previews, so the panel-space travel is converted
        private void OnAvatarDragged(Vector2 panelPosition, Vector2 panelDelta)
        {
            viewInstance!.AvatarDragCursor.MoveToPointer(panelPosition, Vector2.zero);

            VisualElement hitArea = viewInstance.AvatarHitArea;
            Vector2 screenPosition = hitArea.ScreenPosition(panelPosition);
            Vector2 screenDelta = screenPosition - hitArea.ScreenPosition(panelPosition - panelDelta);
            avatarPreview!.Drag(new CharacterPreviewPointerInput(PointerEventData.InputButton.Left, screenPosition, screenDelta));
        }

        private void OnAvatarDragEnded(Vector2 panelPosition)
        {
            viewInstance!.AvatarDragCursor.SetDisplayed(false);
            cursor.SetVisibility(true);

            // No enter event follows a captured drag, so a release over the figure brings the hint back here
            VisualElement hitArea = viewInstance.AvatarHitArea;
            if (!hitArea.ContainsPoint(hitArea.WorldToLocal(panelPosition))) return;

            VisualElement tooltip = viewInstance.AvatarTooltip;
            tooltip.MoveToPointer(panelPosition, AVATAR_TOOLTIP_OFFSET);
            tooltip.SetDisplayed(true);
        }

        // The origin travels with the handlers so a Jump in from the details is attributed to the card's row
        private LobbyPlacesRail CreatePlacesRail(List<PlacesData.PlaceInfo> places, LobbyCardOrigin origin, PlacesSection viewAllSection) =>
            new (viewInstance!.PlaceCardTemplate)
            {
                CardClicked = index => OnPlaceClicked(places[index], origin),
                CardJumpInClicked = index => OnPlaceJumpIn(places[index], origin),
                ViewAllClicked = () => ShowExploreSection(ExploreSections.Places, viewAllSection),
            };

        // A failed fetch keeps the last successful places, so the rows only hide while none have ever arrived; the four-column
        // rail shows the first of them up to its own cap
        private async UniTaskVoid ShowPlacesAsync(LobbyPlacesRail rail, LobbyPlacesRail rail4By2, List<PlacesData.PlaceInfo> places, UniTask<PlacesData.IPlacesAPIResponse> fetch, int maxCount, int maxCount4By2, CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await fetch.SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                places.Clear();
                IReadOnlyList<PlacesData.PlaceInfo> data = result.Value.Data;

                for (var i = 0; i < data.Count && i < maxCount; i++)
                    places.Add(data[i]);
            }

            ShowPlaceCards(rail, places, places.Count, ct);
            ShowPlaceCards(rail4By2, places, Mathf.Min(places.Count, maxCount4By2), ct);
        }

        private void ShowPlaceCards(LobbyPlacesRail rail, List<PlacesData.PlaceInfo> places, int count, CancellationToken ct)
        {
            rail.SetCount(count);

            for (var i = 0; i < count; i++)
                ShowPlaceCard(rail.Cards[i], places[i], places[i].connected_addresses, ct);
        }

        private void ShowPlaceCard(LobbyPlaceCardElement card, PlacesData.PlaceInfo place, string[]? connectedAddresses, CancellationToken ct)
        {
            card.Title = place.title;
            card.Creator = place.contact_name;

            // Only the endpoints resolving connected users return addresses; the aggregated count is the fallback
            card.OnlineCount = connectedAddresses?.Length ?? place.user_count;
            BindConnectedFriends(card.ConnectedFriends, connectedAddresses);

            LoadThumbnailAsync(card, place.image, ReportCategory.PLACES, ct).Forget();
        }

        private void BindConnectedFriends(LobbyConnectedFriendsElement? friendsRow, string[]? addresses)
        {
            if (connectedFriends != null && friendsRow != null)
                connectedFriends.Bind(friendsRow, addresses);
        }

        private async UniTaskVoid LoadThumbnailAsync(LobbyThumbnailCardElement card, string? url, string reportCategory, CancellationToken ct)
        {
            card.Thumbnail = null;

            if (string.IsNullOrEmpty(url))
            {
                card.IsLoading = false;
                return;
            }

            card.IsLoading = true;
            Sprite? sprite = null;

            try { sprite = await spriteCache.GetSpriteAsync(url, useKtx: true, ct: ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, reportCategory); }

            // The cache returns rather than throws on a cancellation
            if (ct.IsCancellationRequested) return;

            card.IsLoading = false;
            card.Thumbnail = sprite;
        }

        // A failed fetch keeps the last communities, so the banner only shows while none have ever arrived; a user the
        // feature flag leaves out gets none
        private async UniTaskVoid ShowCommunitiesAsync(CancellationToken ct)
        {
            if (communitiesProvider == null) return;

            Result<bool> allowed = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested || !allowed.Success || !allowed.Value) return;

            ShowCommunitiesSections();

            Result<GetUserCommunitiesResponse> result = await communitiesProvider.GetUserCommunitiesAsync(string.Empty, onlyMemberOf: true, pageNumber: 1, elementsPerPage: MAX_COMMUNITIES, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                communities.Clear();
                GetUserCommunitiesData.CommunityData[] data = result.Value.data.results;

                for (var i = 0; i < data.Length && i < MAX_COMMUNITIES; i++)
                    communities.Add(data[i]);
            }

            ShowCommunityCards(communitiesRail!, ct);
            ShowCommunityCards(communities4By2Rail!, ct);
        }

        private void ShowCommunitiesSections()
        {
            if (communitiesSectionsShown) return;

            communitiesSectionsShown = true;
            communitiesRail!.Show(viewInstance!.Communities);
            communities4By2Rail!.Show(viewInstance.Communities4By2);
        }

        private void ShowCommunityCards(LobbyCommunitiesRail rail, CancellationToken ct)
        {
            rail.SetCount(communities.Count);

            for (var i = 0; i < communities.Count; i++)
                ShowCommunityCard(rail.Cards[i], communities[i], ct);
        }

        private void ShowCommunityCard(LobbyCommunityCardElement card, GetUserCommunitiesData.CommunityData community, CancellationToken ct)
        {
            card.Title = community.name;
            card.MembersCount = community.membersCount;
            card.IsPrivate = community.privacy == CommunityPrivacy.@private;

            LoadThumbnailAsync(card, community.ThumbnailUrl, ReportCategory.COMMUNITIES, ct).Forget();
        }

        // The card panel reads the pictures from the same cache, so the thumbnail it opens with is already there
        private void OnCommunityClicked(int index) =>
            mvcManager.ShowAndForget(CommunityCardController.IssueCommand(new CommunityCardParameter(communities[index].id, spriteCache)));

        private void SubscribeToCommunityChanges()
        {
            if (communitiesProvider == null) return;

            communitiesProvider.CommunityCreated += OnCommunityCreated;
            communitiesProvider.CommunityDeleted += OnCommunityChanged;
            communitiesProvider.CommunityUpdated += OnCommunityChanged;
            communitiesProvider.CommunityUserRemoved += OnCommunityChanged;
            communitiesProvider.CommunityJoined += OnCommunityMembershipChanged;
            communitiesProvider.CommunityLeft += OnCommunityMembershipChanged;
        }

        private void UnsubscribeFromCommunityChanges()
        {
            if (communitiesProvider == null) return;

            communitiesProvider.CommunityCreated -= OnCommunityCreated;
            communitiesProvider.CommunityDeleted -= OnCommunityChanged;
            communitiesProvider.CommunityUpdated -= OnCommunityChanged;
            communitiesProvider.CommunityUserRemoved -= OnCommunityChanged;
            communitiesProvider.CommunityJoined -= OnCommunityMembershipChanged;
            communitiesProvider.CommunityLeft -= OnCommunityMembershipChanged;
        }

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData _) =>
            RefetchCommunities();

        private void OnCommunityChanged(string _) =>
            RefetchCommunities();

        private void OnCommunityMembershipChanged(string _, bool success)
        {
            if (success)
                RefetchCommunities();
        }

        // Refetched rather than patched: the rails list the server's order and member counts
        private void RefetchCommunities()
        {
            communitiesCts = communitiesCts.SafeRestart();
            ShowCommunitiesAsync(communitiesCts.Token).Forget();
        }

        // A failed fetch keeps the last photos, so the banner only shows while none have ever arrived; the reel lists
        // the latest first, as the rails do
        private async UniTaskVoid ShowGalleryAsync(CancellationToken ct)
        {
            if (cameraReel == null) return;

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null) return;

            Result<CameraReelResponsesCompact> result = await cameraReel.StorageService.GetCompactScreenshotGalleryAsync(identity.Address, MAX_PHOTOS, 0, ct)
                                                                       .SuppressToResultAsync(ReportCategory.CAMERA_REEL);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                photos.Clear();
                List<CameraReelResponseCompact> images = result.Value.images;

                for (var i = 0; i < images.Count && i < MAX_PHOTOS; i++)
                    photos.Add(images[i]);
            }

            ShowPhotoCards(galleryRail!, ct, rewind: true);
            ShowPhotoCards(gallery4By2Rail!, ct, rewind: true);
        }

        private void ShowPhotoCards(LobbyGalleryRail rail, CancellationToken ct, bool rewind)
        {
            rail.SetCount(photos.Count, rewind);

            for (var i = 0; i < photos.Count; i++)
                LoadThumbnailAsync(rail.Cards[i], photos[i].thumbnailUrl, ReportCategory.CAMERA_REEL, ct).Forget();
        }

        // The detail pages through the same list the rails show, so its arrows stay within the lobby's photos
        private void OnPhotoClicked(int index) =>
            mvcManager.ShowAndForget(PhotoDetailController.IssueCommand(new PhotoDetailParameter(photos, index, false,
                PhotoDetailParameter.CallerContext.CameraReel, OnPhotoDeleteIntention, RemovePhoto, cameraReel!.GalleryEventBus)));

        private void OnPhotoDeleteIntention(CameraReelResponseCompact photo)
        {
            deletePhotoCts = deletePhotoCts.SafeRestart();
            DeletePhotoAsync(photo, deletePhotoCts.Token).Forget();
        }

        // Outlives the show: the detail closes itself on the intention, so the server has to hear of it even if the lobby
        // goes down meanwhile
        private async UniTaskVoid DeletePhotoAsync(CameraReelResponseCompact photo, CancellationToken ct)
        {
            Result<CameraReelStorageStatus> result = await cameraReel!.StorageService.DeleteScreenshotAsync(photo.id, ct)
                                                                     .SuppressToResultAsync(ReportCategory.CAMERA_REEL);

            if (ct.IsCancellationRequested || !result.Success) return;

            RemovePhoto(photo);
            cameraReel.GalleryEventBus.ReelDeleted(photo.id);
        }

        // Keeps the page the rails are on; a hidden lobby only drops the photo from the list, the next show refetches
        private void RemovePhoto(CameraReelResponseCompact photo)
        {
            if (!photos.Remove(photo) || galleryCts == null) return;

            ShowPhotoCards(galleryRail!, galleryCts.Token, rewind: false);
            ShowPhotoCards(gallery4By2Rail!, galleryCts.Token, rewind: false);
        }

        private void SubscribeToReelDeletions()
        {
            if (cameraReel == null) return;

            cameraReel.GalleryEventBus.ReelDeletedEvent += OnReelDeleted;
        }

        private void UnsubscribeFromReelDeletions()
        {
            if (cameraReel == null) return;

            cameraReel.GalleryEventBus.ReelDeletedEvent -= OnReelDeleted;
        }

        // A photo deleted from a panel over the lobby, which lists it too
        private void OnReelDeleted(string reelId)
        {
            for (var i = 0; i < photos.Count; i++)
            {
                if (photos[i].id != reelId) continue;

                RemovePhoto(photos[i]);
                return;
            }
        }

        private LobbyUpcomingEventsRail CreateUpcomingEventsRail() =>
            new (viewInstance!.UpcomingEventCardTemplate)
            {
                CardClicked = OnUpcomingEventClicked,
                CardInterestedClicked = OnUpcomingEventInterested,
                CardAddToCalendarClicked = OnUpcomingEventAddToCalendar,
                CardShareClicked = OnUpcomingEventShare,
                ViewAllClicked = ShowEvents,
            };

        // A failed fetch keeps the last upcoming events but drops the live ones: a stale LIVE badge is a false claim
        private async UniTaskVoid ShowEventsAsync(CancellationToken ct)
        {
            Result<IReadOnlyList<EventDTO>> result = await eventsApiService.GetEventsAsync(ct, withConnectedUsers: true)
                                                                           .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                liveEvents.Clear();
                upcomingEvents.Clear();
                IReadOnlyList<EventDTO> events = result.Value;

                for (var i = 0; i < events.Count; i++)
                    (events[i].live ? liveEvents : upcomingEvents).Add(events[i]);

                // The schedule is not guaranteed to come sorted
                upcomingEvents.Sort(BY_START_TIME);

                if (upcomingEvents.Count > MAX_UPCOMING_EVENTS)
                    upcomingEvents.RemoveRange(MAX_UPCOMING_EVENTS, upcomingEvents.Count - MAX_UPCOMING_EVENTS);
            }
            else
                liveEvents.Clear();

            liveEventsRail!.SetCount(liveEvents.Count);

            for (var i = 0; i < liveEvents.Count; i++)
                ShowLiveEventCard(liveEventsRail.Cards[i], liveEvents[i], ct);

            upcomingEventsRail!.SetCount(upcomingEvents.Count);

            for (var i = 0; i < upcomingEvents.Count; i++)
                ShowUpcomingEventCard(upcomingEventsRail.Cards[i], upcomingEvents[i], ct);

            if (!result.Success) return;

            eventPlacesResolved = ResolveEventPlacesAsync(ct).Preserve();
            await eventPlacesResolved;
        }

        private async UniTask ResolveEventPlacesAsync(CancellationToken ct)
        {
            eventPlaces.Clear();
            eventPlaceIds.Clear();
            CollectEventPlaceIds(liveEvents);
            CollectEventPlaceIds(upcomingEvents);

            if (eventPlaceIds.Count == 0) return;

            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetDestinationsByIdsAsync(eventPlaceIds, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested || !result.Success) return;

            IReadOnlyList<PlacesData.PlaceInfo> places = result.Value.Data;

            for (var i = 0; i < places.Count; i++)
                eventPlaces[places[i].id] = places[i];
        }

        private void CollectEventPlaceIds(List<EventDTO> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                string placeId = events[i].place_id;

                if (!string.IsNullOrEmpty(placeId) && !eventPlaceIds.Contains(placeId))
                    eventPlaceIds.Add(placeId);
            }
        }

        private PlacesData.PlaceInfo? EventPlace(string? placeId) =>
            !string.IsNullOrEmpty(placeId) && eventPlaces.TryGetValue(placeId, out PlacesData.PlaceInfo? place) ? place : null;

        private void ShowLiveEventCard(LobbyLiveEventCardElement card, EventDTO @event, CancellationToken ct)
        {
            card.Title = @event.name;
            card.Host = string.Format(LIVE_EVENT_HOST_FORMAT, @event.user_name);
            card.Attendees = @event.connected_addresses?.Length ?? 0;
            BindConnectedFriends(card.ConnectedFriends, @event.connected_addresses);

            LoadThumbnailAsync(card, @event.image, ReportCategory.EVENTS, ct).Forget();
        }

        private void ShowUpcomingEventCard(LobbyUpcomingEventCardElement card, EventDTO @event, CancellationToken ct)
        {
            card.Title = @event.name;
            card.Host = @event.user_name;
            card.StartsIn = EventUtilities.GetEventStartsInText(@event);
            card.IsInterested = @event.attending;

            LoadThumbnailAsync(card, @event.image, ReportCategory.EVENTS, ct).Forget();
        }

        private void OnPlaceClicked(PlacesData.PlaceInfo place, LobbyCardOrigin origin)
        {
            PlaceOpened?.Invoke(place, origin);
            mvcManager.ShowAndForget(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(place, jumpInHandler: jumped => OnPlaceJumpIn(jumped, origin))));
        }

        private void OnPlaceJumpIn(PlacesData.PlaceInfo place, LobbyCardOrigin origin)
        {
            PlaceJumpedIn?.Invoke(place, origin);

            if (place.IsWorld)
                PickWorld(WorldUrl(place.world_name));
            else
                PickParcel(place.base_position_processed, landOnParcel: false);
        }

        private void OnLiveEventClicked(int index) =>
            OpenEventDetails(liveEvents[index], LobbyCardOrigin.LiveEvents);

        private void OnLiveEventJumpInClicked(int index) =>
            OnEventJumpIn(liveEvents[index], LobbyCardOrigin.LiveEvents);

        private void OnUpcomingEventClicked(int index) =>
            OpenEventDetails(upcomingEvents[index], LobbyCardOrigin.UpcomingEvents);

        private void OnUpcomingEventInterested(int index)
        {
            if (eventsCts == null) return;

            eventCardActions.SetEventAsInterestedAsync(upcomingEvents[index], null, null, eventsCts.Token).Forget();
        }

        // Interest is toggled on a boxed copy of the event, so the listed event and its card are brought in line by id
        private void OnEventInterestChanged(IEventDTO @event)
        {
            for (var i = 0; i < upcomingEvents.Count; i++)
            {
                if (upcomingEvents[i].id != @event.Id) continue;

                EventDTO listed = upcomingEvents[i];
                listed.attending = @event.Attending;
                upcomingEvents[i] = listed;
                upcomingEventsRail!.Cards[i].IsInterested = @event.Attending;
            }
        }

        private void OnUpcomingEventAddToCalendar(int index) =>
            eventCardActions.AddEventToCalendar(upcomingEvents[index]);

        // The menu is uGUI, so it is anchored to the button in screen pixels
        private void OnUpcomingEventShare(int index)
        {
            sharedEvent = upcomingEvents[index];
            Vector2 anchor = upcomingEventsRail!.Cards[index].ShareButtonScreenPosition;
            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(ShareMenu(), anchor)));
        }

        private GenericContextMenu ShareMenu()
        {
            if (shareMenu != null)
                return shareMenu;

            EventContextMenuConfiguration settings = viewInstance!.EventContextMenuSettings;

            shareMenu = new GenericContextMenu(settings.ContextMenuWidth, settings.OffsetFromTarget, settings.VerticalPadding, settings.ElementsSpacing)
                       .AddControl(new ButtonContextMenuControlSettings(settings.ShareText, settings.ShareSprite, ShareEvent))
                       .AddControl(new ButtonContextMenuControlSettings(settings.CopyLinkText, settings.CopyLinkSprite, CopyEventLink));

            return shareMenu;
        }

        private void ShareEvent() =>
            eventCardActions.ShareEvent(sharedEvent);

        private void CopyEventLink() =>
            eventCardActions.CopyEventLink(sharedEvent);

        private void OpenEventDetails(EventDTO @event, LobbyCardOrigin origin)
        {
            if (eventsCts == null) return;

            EventOpened?.Invoke(@event, origin);
            ShowEventDetailsAsync(@event, @event.place_id, origin, eventsCts.Token).Forget();
        }

        // The details toggle interest on their boxed copy, so the listed event is synced from it once they close
        private async UniTaskVoid ShowEventDetailsAsync(IEventDTO @event, string? placeId, LobbyCardOrigin origin, CancellationToken ct)
        {
            await eventPlacesResolved;

            if (ct.IsCancellationRequested) return;

            try { await mvcManager.ShowAsync(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, EventPlace(placeId), jumpInHandler: jumped => OnEventJumpIn(jumped, origin)))); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.EVENTS); return; }

            if (ct.IsCancellationRequested) return;

            OnEventInterestChanged(@event);
        }

        // Land on the event's parcel rather than on the scene spawn point
        private void OnEventJumpIn(IEventDTO @event, LobbyCardOrigin origin)
        {
            EventJumpedIn?.Invoke(@event, origin);

            if (@event.World)
                PickWorld(WorldUrl(@event.Server));
            else
                PickParcel(new Vector2Int(@event.X, @event.Y), landOnParcel: true);
        }

        // Land next to the friend rather than on the scene spawn point
        private void OnFriendJoin(OnlineUserData friend)
        {
            Vector2Int parcel = friend.position.ToParcel();

            FriendJoined?.Invoke(friend.avatarId, parcel);

            if (friend.worldName is { Length: > 0 } worldName)
                PickWorld(WorldUrl(worldName));
            else
                PickParcel(parcel, landOnParcel: true);
        }

        // The row takes what the /goto chat command takes: a parcel or a world, each with an optional spawn point and the
        // world with an optional parcel, or the random and crowd keywords
        private void OnGoToSubmitted(string destination)
        {
            GotoTarget target = ChatParamUtils.ParseGotoTarget(destination);

            if (target.World is { } worldName)
            {
                goToCts = goToCts.SafeRestart();
                GoToWorldAsync(destination, target, WorldUrl(WorldName(worldName)), goToCts.Token).Forget();
            }
            else
                GoTo(destination, target);
        }

        // Probed as the realm change does, so a world that does not exist is reported on the pill, not after a fallback to Genesis
        private async UniTaskVoid GoToWorldAsync(string destination, GotoTarget target, URLDomain worldUrl, CancellationToken ct)
        {
            LobbyGoToElement goTo = viewInstance!.GoToField;
            goTo.IsChecking = true;

            bool reachable = await webRequestController.IsHeadReachableAsync(ReportCategory.REALM, worldUrl.Append(new URLPath(WORLD_ABOUT_PATH)), ct, suppressErrors: true);

            goTo.IsChecking = false;

            if (ct.IsCancellationRequested) return;

            if (!reachable)
            {
                goTo.ShowInvalid();
                return;
            }

            GoTo(destination, target);
        }

        private void GoTo(string destination, GotoTarget target)
        {
            viewInstance!.GoToField.Text = string.Empty;

            // In-world the destination is sent to the chat as the command itself, which resolves it like a typed /goto
            // and reports the outcome in the nearby channel
            if (startParcel.IsConsumed())
            {
                chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {destination}", ChatMessageOrigin.JumpIn);
                RequestClose();
                return;
            }

            if (target.IsCrowd)
                StartAtCrowdAsync(placesCts!.Token).Forget();
            else if (target.IsRandom)
                StartAt(GenesisUrl(), GoToChatCommand.RandomParcel(), spawnPointName: null);
            else if (target.World != null)
                StartAt(WorldUrl(WorldName(target.World)), target.Parcel, target.SpawnPoint);
            else if (target.Parcel is { } parcel)
                StartAt(GenesisUrl(), parcel, target.SpawnPoint);
        }

        // The lookup runs on the places token, which closing the lobby cancels; a lookup that fails is reported and leaves
        // the lobby up
        private async UniTaskVoid StartAtCrowdAsync(CancellationToken ct)
        {
            Result<Vector2Int> result = await GoToChatCommand.FindCrowdAsync(webRequestController, decentralandUrlsSource, ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested) return;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(CROWD_LOOKUP_ERROR_MESSAGE));
                return;
            }

            StartAt(GenesisUrl(), result.Value, spawnPointName: null);
        }

        // Before the world loads the startup teleport lands in the pick; in-world it teleports right away, onto the world's
        // own spawn
        private void PickWorld(URLDomain worldUrl)
        {
            if (!startParcel.IsConsumed())
            {
                StartAt(worldUrl, parcel: null, spawnPointName: null);
                return;
            }

            jumpInCts = jumpInCts.SafeRestart();
            realmNavigator.TryChangeRealmAsync(worldUrl, jumpInCts.Token, isWorld: true, allowsSpawnPointerOverride: true).Forget();
            RequestClose();
        }

        // Before the world loads the startup teleport lands in the pick; in-world it teleports right away
        private void PickParcel(Vector2Int parcel, bool landOnParcel)
        {
            if (!startParcel.IsConsumed())
            {
                StartAt(GenesisUrl(), parcel, spawnPointName: null, landOnParcel);
                return;
            }

            jumpInCts = jumpInCts.SafeRestart();
            realmNavigator.TeleportToParcelAsync(parcel, jumpInCts.Token, false, landOnParcel).Forget();
            RequestClose();
        }

        // Hands the pick to the startup teleport: the realm, the parcel if one is given and the realm's own spawn otherwise,
        // and the spawn point if one is named
        private void StartAt(URLDomain realm, Vector2Int? parcel, string? spawnPointName, bool landOnParcel = false)
        {
            startParcel.AssignRealm(realm, spawnPointName);

            if (parcel is { } startAt)
                startParcel.Assign(startAt, spawnPointName, landOnParcel);

            RequestClose();
        }

        private URLDomain GenesisUrl() =>
            URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis));

        private URLDomain WorldUrl(string worldName) =>
            URLDomain.FromString(new ENS(worldName).ConvertEnsToWorldUrl(decentralandUrlsSource.Url(DecentralandUrl.WorldServer)));

        // A short name is a .dcl.eth world; a full ENS name stands as typed
        private static string WorldName(string name) =>
            name.IsEns() || name.EndsWith(WORLD_SUFFIX, StringComparison.OrdinalIgnoreCase) ? name : name + WORLD_SUFFIX;

        private void OnUIClose(InputAction.CallbackContext _) =>
            profileMenu.Dismiss();

        private void ShowSettings() =>
            ShowExploreSection(ExploreSections.Settings);

        private void ShowNotifications() =>
            ShowNotificationsAsync().Forget();

        // A click while the panel is up restarts the show, as in the sidebar
        private async UniTaskVoid ShowNotificationsAsync()
        {
            CancellationTokenSource cts = notificationsCts = notificationsCts.SafeRestart();
            viewInstance!.SetNotificationsPanelOpen(true);

            try { await mvcManager.ShowAsync(NotificationsPanelController<LobbyPopupParameter>.IssueCommand(new LobbyPopupParameter()), cts.Token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
            finally
            {
                // A restart owns the state from then on
                if (ReferenceEquals(cts, notificationsCts))
                    viewInstance.SetNotificationsPanelOpen(false);
            }
        }

        private void ShowMap() =>
            ShowExploreSection(ExploreSections.Navmap);

        private void ShowPlaces() =>
            ShowExploreSection(ExploreSections.Places);

        private void ShowEvents() =>
            ShowExploreSection(ExploreSections.Events);

        private void ShowCommunities() =>
            ShowExploreSection(ExploreSections.Communities);

        private void ShowGallery() =>
            ShowExploreSection(ExploreSections.CameraReel);

        private void ShowExploreSection(ExploreSections section, PlacesSection? placesSection = null)
        {
            ExploreSectionOpened?.Invoke(section);
            mvcManager.ShowAndForget(ExploreSectionModalController.IssueCommand(new ExploreSectionModalParameter(section, placesSection)));
        }

        private void RequestClose()
        {
            // Leaving rather than being covered: the startup lobby must not show itself again
            leaving = true;
            inputData.JumpedIn?.Invoke();

            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }
}
