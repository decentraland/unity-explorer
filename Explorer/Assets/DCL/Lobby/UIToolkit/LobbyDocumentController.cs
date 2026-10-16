using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.CharacterPreview;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.Events;
using DCL.EventsApi;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Notifications.NotificationsMenu;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const int MAX_UPCOMING_EVENTS = 10;

        private const int MAX_RECENT_PLACES = 3;

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
        private readonly StartParcel startParcel;
        private readonly ISpriteCache spriteCache;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly NotificationsPanelController<LobbyPopupParameter> notificationsPanel;
        private readonly List<PlacesData.PlaceInfo> recentPlaces = new ();
        private readonly List<PlacesData.PlaceInfo> featuredPlaces = new ();
        private readonly List<EventDTO> liveEvents = new ();
        private readonly List<EventDTO> upcomingEvents = new ();
        private readonly LobbyDocumentFriendsPresenter? friends;
        private readonly LobbyConnectedFriendsPresenter? connectedFriends;
        private readonly ClickOrDragManipulator avatarGesture;

        private LobbyPlacesRail? recentPlacesRail;
        private LobbyPlacesRail? featuredPlacesRail;
        private LobbyLiveEventsRail? liveEventsRail;
        private LobbyUpcomingEventsRail? upcomingEventsRail;
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

        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
        private CancellationTokenSource? eventsCts;
        private CancellationTokenSource? friendsCts;
        private CancellationTokenSource? jumpInCts;
        private UniTaskCompletionSource? closeIntent;
        private bool leaving;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // At startup the lobby is the only way into the world, so it cannot be dismissed before a destination is picked
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;

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
            StartParcel startParcel,
            ISpriteCache spriteCache,
            SidebarProfileButtonPresenter profileButtonPresenter,
            NotificationsPanelController<LobbyPopupParameter> notificationsPanel,
            LobbyDocumentFriendsPresenter? friends,
            LobbyConnectedFriendsPresenter? connectedFriends) : base(viewFactory)
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
            this.startParcel = startParcel;
            this.spriteCache = spriteCache;
            this.profileButtonPresenter = profileButtonPresenter;
            this.notificationsPanel = notificationsPanel;
            this.friends = friends;
            this.connectedFriends = connectedFriends;

            avatarGesture = new ClickOrDragManipulator
            {
                Clicked = OnAvatarClicked,
                DragStarted = OnAvatarDragStarted,
                Dragged = OnAvatarDragged,
                DragEnded = OnAvatarDragEnded,
            };

            if (friends != null)
                friends.JoinRequested = OnFriendJoin;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
                viewInstance.Profile.Clicked = null;

            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;
            startParcel.JumpInRequestRaised -= RequestClose;
            eventCardActions.EventSetAsInterested -= OnEventInterestChanged;
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            notificationsPanel.UnreadCountChanged -= ShowUnreadCount;

            if (friends != null)
                friends.JoinRequested = null;

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
            friendsCts.SafeCancelAndDispose();
            jumpInCts.SafeCancelAndDispose();
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
            viewInstance.Profile.Clicked = ShowProfileMenu;
            viewInstance.NotificationsButton.clicked -= ShowNotifications;
            viewInstance.NotificationsButton.clicked += ShowNotifications;
            viewInstance.CloseButton.clicked -= RequestClose;
            viewInstance.CloseButton.clicked += RequestClose;
            viewInstance.CloseButton.SetDisplayed(!inputData.IsStartup);
            profileButtonPresenter.LoadProfile();

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

            recentPlacesRail ??= CreatePlacesRail(recentPlaces, LobbyCardOrigin.Recent);
            featuredPlacesRail ??= CreatePlacesRail(featuredPlaces, LobbyCardOrigin.Recommended);
            recentPlacesRail.Show(viewInstance.RecentPlaces);
            featuredPlacesRail.Show(viewInstance.FeaturedPlaces);

            liveEventsRail ??= new LobbyLiveEventsRail(viewInstance.LiveEventCardTemplate) { CardClicked = OnLiveEventClicked };
            upcomingEventsRail ??= CreateUpcomingEventsRail();
            liveEventsRail.Show(viewInstance.LiveEvents);
            upcomingEventsRail.Show(viewInstance.UpcomingEvents);

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
            ShowPlacesAsync(recentPlacesRail, recentPlaces, placesAPIService.GetRecentlyVisitedDestinationsAsync(placesCts.Token, withConnectedUsers: true), MAX_RECENT_PLACES, placesCts.Token).Forget();
            ShowPlacesAsync(featuredPlacesRail, featuredPlaces, placesAPIService.GetHighlightedDestinationsAsync(placesCts.Token, withConnectedUsers: true), int.MaxValue, placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            eventCardActions.EventSetAsInterested += OnEventInterestChanged;
            ShowEventsAsync(eventsCts.Token).Forget();

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
            notificationsPanel.UnreadCountChanged -= ShowUnreadCount;

            // Nulled so a late callback finds no source rather than a disposed one, whose token throws
            avatarCts.SafeCancelAndDispose();
            avatarCts = null;
            placesCts.SafeCancelAndDispose();
            placesCts = null;
            eventsCts.SafeCancelAndDispose();
            eventsCts = null;
            friends?.Hide();
            connectedFriends?.Hide();
            friendsCts.SafeCancelAndDispose();
            friendsCts = null;

            // A drag cut short by the close leaves the hardware cursor hidden otherwise
            avatarGesture.Cancel();
            avatarPreview!.OnHide();

            recentPlacesRail?.Hide();
            featuredPlacesRail?.Hide();
            liveEventsRail?.Hide();
            upcomingEventsRail?.Hide();

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
            if (startParcel.IsConsumed())
                OnPlaceJumpIn(landingPlace, LobbyCardOrigin.Landing);
            else
            {
                PlaceJumpedIn?.Invoke(landingPlace, LobbyCardOrigin.Landing);
                RequestClose();
            }
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
        private LobbyPlacesRail CreatePlacesRail(List<PlacesData.PlaceInfo> places, LobbyCardOrigin origin) =>
            new (viewInstance!.PlaceCardTemplate)
            {
                CardClicked = index => OnPlaceClicked(places[index], origin),
                CardJumpInClicked = index => OnPlaceJumpIn(places[index], origin),
            };

        // A failed fetch keeps the last successful places, so the row only hides while none have ever arrived
        private async UniTaskVoid ShowPlacesAsync(LobbyPlacesRail rail, List<PlacesData.PlaceInfo> places, UniTask<PlacesData.IPlacesAPIResponse> fetch, int maxCount, CancellationToken ct)
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

            rail.SetCount(places.Count);

            for (var i = 0; i < places.Count; i++)
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

        private LobbyUpcomingEventsRail CreateUpcomingEventsRail() =>
            new (viewInstance!.UpcomingEventCardTemplate)
            {
                CardClicked = OnUpcomingEventClicked,
                CardInterestedClicked = OnUpcomingEventInterested,
                CardAddToCalendarClicked = OnUpcomingEventAddToCalendar,
                CardShareClicked = OnUpcomingEventShare,
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

            viewInstance!.Events.SetDisplayed(liveEvents.Count > 0 || upcomingEvents.Count > 0);
        }

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
            PickDestination(place.IsWorld ? WorldUrl(place.world_name) : null, place.base_position_processed, landOnParcel: false);
        }

        private void OnLiveEventClicked(int index) =>
            OpenEventDetails(liveEvents[index], LobbyCardOrigin.LiveEvents);

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
            ShowEventDetailsAsync(@event, origin, eventsCts.Token).Forget();
        }

        // The details toggle interest on their boxed copy, so the listed event is synced from it once they close
        private async UniTaskVoid ShowEventDetailsAsync(IEventDTO @event, LobbyCardOrigin origin, CancellationToken ct)
        {
            try { await mvcManager.ShowAsync(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, placeData: null, jumpInHandler: jumped => OnEventJumpIn(jumped, origin)))); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.EVENTS); return; }

            if (ct.IsCancellationRequested) return;

            OnEventInterestChanged(@event);
        }

        // Land on the event's parcel rather than on the scene spawn point
        private void OnEventJumpIn(IEventDTO @event, LobbyCardOrigin origin)
        {
            EventJumpedIn?.Invoke(@event, origin);
            PickDestination(@event.World ? WorldUrl(@event.Server) : null, new Vector2Int(@event.X, @event.Y), landOnParcel: true);
        }

        // Land next to the friend rather than on the scene spawn point
        private void OnFriendJoin(OnlineUserData friend)
        {
            Vector2Int parcel = friend.position.ToParcel();

            FriendJoined?.Invoke(friend.avatarId, parcel);
            PickDestination(friend.worldName is { Length: > 0 } worldName ? WorldUrl(worldName) : null, parcel, landOnParcel: true);
        }

        // Before the world loads the startup teleport lands in the pick; in-world it teleports right away
        private void PickDestination(URLDomain? worldUrl, Vector2Int parcel, bool landOnParcel)
        {
            if (startParcel.IsConsumed())
            {
                jumpInCts = jumpInCts.SafeRestart();

                if (worldUrl.HasValue)
                    realmNavigator.TryChangeRealmAsync(worldUrl.Value, jumpInCts.Token, isWorld: true, allowsSpawnPointerOverride: true).Forget();
                else
                    realmNavigator.TeleportToParcelAsync(parcel, jumpInCts.Token, false, landOnParcel).Forget();
            }
            else if (worldUrl.HasValue)
                startParcel.AssignRealm(worldUrl.Value);
            else
            {
                startParcel.AssignRealm(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)));
                startParcel.Assign(parcel);
            }

            RequestClose();
        }

        private URLDomain WorldUrl(string worldName) =>
            URLDomain.FromString(new ENS(worldName).ConvertEnsToWorldUrl(decentralandUrlsSource.Url(DecentralandUrl.WorldServer)));

        private void ShowProfileMenu() =>
            mvcManager.ShowAndForget(ProfileMenuController<LobbyPopupParameter>.IssueCommand(new LobbyPopupParameter()));

        private void ShowNotifications() =>
            mvcManager.ShowAndForget(NotificationsPanelController<LobbyPopupParameter>.IssueCommand(new LobbyPopupParameter()));

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
