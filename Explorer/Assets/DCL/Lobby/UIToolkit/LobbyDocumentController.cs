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
using UnityEngine.UIElements;
using Utility;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world loads and on demand in-world. It only reports the close intent; what follows is up to the caller.
    /// </summary>
    public class LobbyDocumentController : ControllerBase<LobbyDocumentView, LobbyParameter>
    {
        private const string LIVE_EVENT_HOST_FORMAT = "By {0}";
        private const string WELCOME_FALLBACK = "Welcome!";
        private const string WELCOME_FORMAT = "Welcome {0}!";
        private const int MAX_UPCOMING_EVENTS = 10;

        // The recent row holds the latest few in a plain row; the featured rail pages through them all
        private const int MAX_RECENT_PLACES = 3;

        private static readonly Comparison<EventDTO> BY_START_TIME = static (a, b) => a.NextStartAtProcessed.CompareTo(b.NextStartAtProcessed);

        private readonly IInputBlock inputBlock;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
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

        // The rails need the card template of the view, which exists only from the first show on
        private LobbyPlacesRail? recentPlacesRail;
        private LobbyPlacesRail? featuredPlacesRail;
        private LobbyLiveEventsRail? liveEventsRail;
        private LobbyUpcomingEventsRail? upcomingEventsRail;
        private LobbyCharacterPreviewController? avatarPreview;

        // Null while the landing place loads; the card itself knows whether it has details to open
        private PlacesData.PlaceInfo? landingPlace;

        // The destination the shown landing place describes; null together with the place
        private LandingDestination? landingDestination;
        private LandingDestination? startupDestination;

        // One menu serves every Share button; the event it acts on is the one whose button opened it
        private GenericContextMenu? shareMenu;
        private EventDTO sharedEvent;

        // One manipulator for the avatar hit area, added again on every show without stacking
        private Clickable? avatarClick;
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

        /// <summary>
        ///     The panel is on screen. True at the startup show, false when the user opened it from the world.
        /// </summary>
        public event Action<bool>? Opened;

        /// <summary>
        ///     The panel left the screen, once per <see cref="Opened" />.
        /// </summary>
        public event Action? Closed;

        /// <summary>
        ///     A place card was picked, which opens its details rather than jumping in.
        /// </summary>
        public event Action<PlacesData.PlaceInfo, LobbySection>? PlaceOpened;

        /// <summary>
        ///     The user is on their way to a place, from a card's Jump in or from the details that card opened.
        /// </summary>
        public event Action<PlacesData.PlaceInfo, LobbySection>? PlaceJumpedIn;

        /// <summary>
        ///     An event card was picked, which opens its details rather than jumping in.
        /// </summary>
        public event Action<IEventDTO, LobbySection>? EventOpened;

        /// <summary>
        ///     The user is on their way to an event, from the details its card opened.
        /// </summary>
        public event Action<IEventDTO, LobbySection>? EventJumpedIn;

        /// <summary>
        ///     The user is on their way to where a friend is, with the parcel the friend was at.
        /// </summary>
        public event Action<string, Vector2Int>? FriendJoined;

        public LobbyDocumentController(ViewFactoryMethod viewFactory,
            IInputBlock inputBlock,
            IReadOnlyLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
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
            LobbyDocumentFriendsPresenter? friends) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
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

            if (friends != null)
                friends.JoinRequested = OnFriendJoin;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
            {
                viewInstance.Profile.Clicked = null;
                viewInstance.Profile.Dispose();
            }

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

        // The preview is a uGUI view nested in the prefab, so unlike the hierarchy it is there from the instantiation on
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

        // The hierarchy exists from the first show on, so the rows are handed their section here rather than at instantiation
        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            // The renderer keeps the hierarchy across hides, so every handler is removed before it is added and stays registered once;
            // the top-bar widgets outlive the show cycle as well, so their presenters stay bound
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

            // Hidden until the avatar is up again: the area kept from the last show must not take clicks over an empty stage
            VisualElement avatarHitArea = viewInstance.AvatarHitArea;
            avatarHitArea.SetDisplayed(false);
            avatarHitArea.AddManipulator(avatarClick ??= new Clickable(OnAvatarClicked));
            avatarHitArea.RegisterCallback<PointerEnterEvent>(OnAvatarPointerEnter);
            avatarHitArea.RegisterCallback<PointerLeaveEvent>(OnAvatarPointerLeave);

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
            avatarCts = avatarCts.SafeRestart();
            ShowAvatarAsync(avatarCts.Token).Forget();

            LobbyLandingCardElement landingCard = viewInstance.LandingCard;
            landingCard.Clicked = OnLandingCardClicked;
            landingCard.JumpInClicked = OnLandingJumpInClicked;

            recentPlacesRail ??= CreatePlacesRail(recentPlaces, LobbySection.Recent);
            featuredPlacesRail ??= CreatePlacesRail(featuredPlaces, LobbySection.Recommended);
            recentPlacesRail.Show(viewInstance.RecentPlaces);
            featuredPlacesRail.Show(viewInstance.FeaturedPlaces);

            liveEventsRail ??= new LobbyLiveEventsRail(viewInstance.LiveEventCardTemplate) { CardClicked = OnLiveEventClicked };
            upcomingEventsRail ??= CreateUpcomingEventsRail();
            liveEventsRail.Show(viewInstance.LiveEvents);
            upcomingEventsRail.Show(viewInstance.UpcomingEvents);

            placesCts = placesCts.SafeRestart();
            LandingDestination destination = ResolveLandingDestination();

            // A card already showing the destination keeps it up while its details are fetched again, instead of going dark for the round trip
            if (landingDestination is not { } shown || !shown.Equals(destination))
                ShowLandingCardLoading(landingCard);

            ShowLandingPlaceAsync(destination, placesCts.Token).Forget();
            ShowPlacesAsync(recentPlacesRail, recentPlaces, placesAPIService.GetRecentlyVisitedDestinationsAsync(placesCts.Token), MAX_RECENT_PLACES, placesCts.Token).Forget();
            ShowPlacesAsync(featuredPlacesRail, featuredPlaces, placesAPIService.GetHighlightedDestinationsAsync(placesCts.Token), int.MaxValue, placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            eventCardActions.EventSetAsInterested += OnEventInterestChanged;
            ShowEventsAsync(eventsCts.Token).Forget();

            friendsCts = friendsCts.SafeRestart();
            friends?.Show(viewInstance.Friends, friendsCts.Token);

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

            // Nulled so a late callback finds no source instead of reading the token of a disposed one, which throws
            avatarCts.SafeCancelAndDispose();
            avatarCts = null;
            placesCts.SafeCancelAndDispose();
            placesCts = null;
            eventsCts.SafeCancelAndDispose();
            eventsCts = null;
            friends?.Hide();
            friendsCts.SafeCancelAndDispose();
            friendsCts = null;

            // The hit area is hidden again on the next show; the preview and its stage are released here
            avatarPreview!.OnHide();

            recentPlacesRail?.Hide();
            featuredPlacesRail?.Hide();
            liveEventsRail?.Hide();
            upcomingEventsRail?.Hide();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

            // At startup the lobby is the only way into the world, and a fullscreen panel opened from it replaces
            // it instead of closing it: the lobby has to come back, otherwise nothing is left on screen and the flow never resumes
            if (inputData.IsStartup && !leaving)
                mvcManager.OnViewClosed += ShowAgainWhenTheScreenIsFree;

            Closed?.Invoke();
        }

        /// <summary>
        ///     Shows the startup lobby again as soon as the panel that replaced it is gone. A Logout is not a replacement: the
        ///     authentication screen owns the screen from then on, which the cancelled startup token reports.
        /// </summary>
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
                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (ct.IsCancellationRequested) return;

                ShowWelcome(profile);

                if (profile == null)
                {
                    ReportHub.LogWarning(ReportCategory.PROFILE, "Own profile is not available, the lobby avatar is not shown");
                    return;
                }

                avatarPreview!.Initialize(profile.Avatar, CharacterPreviewUtils.LOBBY_PREVIEW_POSITION);
                avatarPreview.OnBeforeShow();
                avatarPreview.OnShow();

                // The hit area only makes sense over a figure, so it comes up with the avatar
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
            card.Title = string.Empty;
            card.Creator = string.Empty;
            card.HasOnlineCount = false;
            card.CanJumpIn = false;
            card.CanOpen = false;
            card.Thumbnail = null;
            card.IsLoading = true;
        }

        /// <summary>
        ///     At startup the hero card's Jump in is the only way out, so an offline stand-in fills it when the Places API cannot describe
        ///     the destination. A card already showing that destination is kept instead when the refetch fails.
        /// </summary>
        private async UniTaskVoid ShowLandingPlaceAsync(LandingDestination destination, CancellationToken ct)
        {
            Result<PlacesData.PlaceInfo?> result = await (destination.WorldName != null
                    ? placesAPIService.GetWorldByNameAsync(destination.WorldName, ct)
                    : placesAPIService.GetPlaceAsync(destination.Parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            // A hide while the picture was loading left the kept card on its loading look, so the picture is loaded again
            if (!result.Success && landingDestination is { } shown && shown.Equals(destination) && landingPlace is { } kept)
            {
                LoadThumbnailAsync(viewInstance!.LandingCard, kept.image, ReportCategory.PLACES, ct).Forget();
                return;
            }

            PlacesData.PlaceInfo? place = result.Success ? result.Value : null;
            ShowLandingCard(destination, place ?? destination.ToOfflinePlace(), hasDetails: place != null, ct);
        }

        /// <summary>
        ///     The launch pick is frozen on first show so the card reads the same all session; only a home destination keeps following the home, which the user may move.
        /// </summary>
        private LandingDestination ResolveLandingDestination()
        {
            startupDestination ??= new LandingDestination(startParcel.Peek(), realmData.IsWorld() ? realmData.RealmName : null);

            if (startParcel.Source != StartParcelSource.Home)
                return startupDestination.Value;

            if (homePlace.IsWorldHome && homePlace.CurrentHomeWorldName is { } homeWorld)
                return new LandingDestination(Vector2Int.zero, homeWorld);

            if (homePlace.CurrentHomeCoordinates is { } homeParcel)
                return new LandingDestination(homeParcel, null);

            // Home was unset during the session: the place the session actually landed in is the closest truth left
            return startupDestination.Value;
        }

        // An offline stand-in carries nothing but the destination itself, so that card only offers Jump in
        private void ShowLandingCard(LandingDestination destination, PlacesData.PlaceInfo place, bool hasDetails, CancellationToken ct)
        {
            landingPlace = place;
            landingDestination = destination;

            LobbyLandingCardElement card = viewInstance!.LandingCard;
            card.HasOnlineCount = true;
            card.CanJumpIn = true;
            card.CanOpen = hasDetails;
            ShowPlaceCard(card, place, ct);
        }

        private void OnLandingCardClicked()
        {
            if (landingPlace != null)
                OnPlaceClicked(landingPlace, LobbySection.Landing);
        }

        private void OnLandingJumpInClicked()
        {
            if (landingPlace == null) return;

            // Before the world loads the card mirrors the destination the launch settings already picked, so there is nothing to reassign
            if (startParcel.IsConsumed())
                OnPlaceJumpIn(landingPlace, LobbySection.Landing);
            else
            {
                PlaceJumpedIn?.Invoke(landingPlace, LobbySection.Landing);
                RequestClose();
            }
        }

        private void OnAvatarClicked() =>
            mvcManager.ShowAndForget(BackpackModalController.IssueCommand(new BackpackModalParameter(BackpackSections.Avatar)));

        private void OnAvatarPointerEnter(PointerEnterEvent evt) =>
            avatarPreview!.SetHovered(true);

        private void OnAvatarPointerLeave(PointerLeaveEvent evt) =>
            avatarPreview!.SetHovered(false);

        // The section travels with the handlers so a Jump in from the details is attributed to the row the card sits in
        private LobbyPlacesRail CreatePlacesRail(List<PlacesData.PlaceInfo> places, LobbySection section) =>
            new (viewInstance!.PlaceCardTemplate)
            {
                CardClicked = index => OnPlaceClicked(places[index], section),
                CardJumpInClicked = index => OnPlaceJumpIn(places[index], section),
            };

        /// <summary>
        ///     Fills a row with the first <paramref name="maxCount" /> places <paramref name="fetch" /> resolves to, one card per place;
        ///     a failed fetch keeps the places of the last one that succeeded, so the row is only hidden while none have ever arrived.
        /// </summary>
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
                ShowPlaceCard(rail.Cards[i], places[i], ct);
        }

        private void ShowPlaceCard(LobbyPlaceCardElement card, PlacesData.PlaceInfo place, CancellationToken ct)
        {
            card.Title = place.title;
            card.Creator = place.contact_name;

            // The addresses come only from the endpoints that resolve connected users; the aggregated count is the fallback
            card.OnlineCount = place.connected_addresses?.Length ?? place.user_count;

            LoadThumbnailAsync(card, place.image, ReportCategory.PLACES, ct).Forget();
        }

        // A cached sprite comes back synchronously, so the loading look is never drawn for it
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

            // The cache returns rather than throws on a cancellation, and the card may already be showing something else
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

        /// <summary>
        ///     A single schedule fetch feeds both event rows. A failed fetch keeps the upcoming events of the last one that succeeded,
        ///     so that row is only hidden while none have ever arrived, but drops the live ones: a stale LIVE badge is a false claim.
        /// </summary>
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

                // The schedule is not guaranteed to come sorted and can be long: keep only the next few
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

        private void OnPlaceClicked(PlacesData.PlaceInfo place, LobbySection section)
        {
            PlaceOpened?.Invoke(place, section);
            mvcManager.ShowAndForget(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(place, jumpInHandler: jumped => OnPlaceJumpIn(jumped, section))));
        }

        private void OnPlaceJumpIn(PlacesData.PlaceInfo place, LobbySection section)
        {
            PlaceJumpedIn?.Invoke(place, section);
            PickDestination(place.IsWorld ? WorldUrl(place.world_name) : null, place.base_position_processed, landOnParcel: false);
        }

        private void OnLiveEventClicked(int index) =>
            OpenEventDetails(liveEvents[index], LobbySection.LiveEvents);

        private void OnUpcomingEventClicked(int index) =>
            OpenEventDetails(upcomingEvents[index], LobbySection.UpcomingEvents);

        private void OnUpcomingEventInterested(int index)
        {
            if (eventsCts == null) return;

            eventCardActions.SetEventAsInterestedAsync(upcomingEvents[index], null, null, eventsCts.Token).Forget();
        }

        /// <summary>
        ///     Interest is toggled on a boxed copy of the event, from the card's button or from the details it opened, so the listed
        ///     event and its card are brought in line by id.
        /// </summary>
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

        private void OpenEventDetails(EventDTO @event, LobbySection section)
        {
            EventOpened?.Invoke(@event, section);
            mvcManager.ShowAndForget(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, placeData: null, jumpInHandler: jumped => OnEventJumpIn(jumped, section))));
        }

        // Land on the exact parcel of the event rather than on the scene spawn point: the event may be held in a corner of a big scene
        private void OnEventJumpIn(IEventDTO @event, LobbySection section)
        {
            EventJumpedIn?.Invoke(@event, section);
            PickDestination(@event.World ? WorldUrl(@event.Server) : null, new Vector2Int(@event.X, @event.Y), landOnParcel: true);
        }

        // Land next to the friend rather than on the scene spawn point
        private void OnFriendJoin(OnlineUserData friend)
        {
            Vector2Int parcel = friend.position.ToParcel();

            FriendJoined?.Invoke(friend.avatarId, parcel);
            PickDestination(friend.worldName is { Length: > 0 } worldName ? WorldUrl(worldName) : null, parcel, landOnParcel: true);
        }

        /// <summary>
        ///     Before the world is loaded the startup teleport lands directly in the picked destination; once in-world it teleports right away.
        ///     Either way the lobby closes.
        /// </summary>
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
