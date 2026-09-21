using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.CharacterPreview;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.Events;
using DCL.EventsApi;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connectivity;
using DCL.Notifications.NotificationsMenu;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
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
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world starts loading and, later, on demand during gameplay.
    ///     It only reports the close intent (Jump in or Close); what happens next is up to the caller.
    ///     Logout is not a close intent: the system menu drives it and the authentication screen replaces this panel.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView, LobbyParameter>
    {
        private const string EVENT_HOST_FORMAT = "By {0}";
        private const int MAX_UPCOMING_EVENTS = 10;

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
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly LobbyFriendsPresenter? friends;
        private readonly List<PlacesData.PlaceInfo> recentPlaces = new ();
        private readonly List<PlacesData.PlaceInfo> recommendedPlaces = new ();
        private readonly List<EventDTO> liveEvents = new ();
        private readonly List<EventDTO> upcomingEvents = new ();

        private LobbyCharacterPreviewController? avatarPreview;
        private PlacesData.PlaceInfo? shownLandingPlace;
        private LandingDestination? startupDestination;
        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
        private CancellationTokenSource? eventsCts;
        private CancellationTokenSource? friendsCts;
        private CancellationTokenSource? jumpInCts;
        private UniTaskCompletionSource? closeIntent;
        private bool leaving;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // Before the world is loaded Jump in is the only way out; once in-world Escape behaves like any other fullscreen panel.
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;

        public LobbyController(ViewFactoryMethod viewFactory,
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
            ThumbnailLoader thumbnailLoader,
            SidebarProfileButtonPresenter profileButtonPresenter,
            LobbyFriendsPresenter? friends) : base(viewFactory)
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
            this.thumbnailLoader = thumbnailLoader;
            this.profileButtonPresenter = profileButtonPresenter;
            this.friends = friends;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
            {
                viewInstance.LandingCard.JumpInButton.Button.onClick.RemoveListener(OnLandingJumpInClicked);
                viewInstance.CloseButton.onClick.RemoveListener(RequestClose);
                viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent -= OnAvatarClicked;
                viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.RemoveListener(ShowProfileMenu);
                viewInstance.NotificationsButton.onClick.RemoveListener(ShowNotifications);

                foreach (LobbyPlaceCardView card in viewInstance.RecentPlaceCards)
                {
                    card.Button.onClick.RemoveAllListeners();
                    card.JumpInButton?.Button.onClick.RemoveAllListeners();
                }

                viewInstance.RecommendedPlaces.CardClicked = null;
                viewInstance.RecommendedPlaces.CardJumpInClicked = null;
                viewInstance.LiveEvents.CardClicked = null;
                viewInstance.UpcomingEvents.CardCreated = null;

                foreach (EventCardView card in viewInstance.UpcomingEvents.Cards)
                    UnsubscribeFromUpcomingCard(card);
            }

            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;

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

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.LandingCard.JumpInButton.Button.onClick.AddListener(OnLandingJumpInClicked);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent += OnAvatarClicked;
            viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.AddListener(ShowProfileMenu);
            viewInstance.NotificationsButton.onClick.AddListener(ShowNotifications);

            LobbyPlaceCardView[] recentCards = viewInstance.RecentPlaceCards;

            for (var i = 0; i < recentCards.Length; i++)
            {
                int index = i;
                recentCards[i].Button.onClick.AddListener(() => OnRecentPlaceClicked(index));
                recentCards[i].JumpInButton?.Button.onClick.AddListener(() => OnRecentPlaceJumpIn(index));
            }

            viewInstance.RecommendedPlaces.CardClicked = OnRecommendedPlaceClicked;
            viewInstance.RecommendedPlaces.CardJumpInClicked = OnRecommendedPlaceJumpIn;
            viewInstance.LiveEvents.CardClicked = OnLiveEventClicked;
            viewInstance.UpcomingEvents.CardCreated = SubscribeToUpcomingCard;

            viewInstance.RecentPlacesSection.SetActive(false);
            viewInstance.RecommendedPlacesSection.SetActive(false);
            viewInstance.EventsSection.SetActive(false);

            if (friends != null)
                friends.JoinRequested = OnFriendJoin;

            avatarPreview = new LobbyCharacterPreviewController(viewInstance.CharacterPreviewView, avatarSettings, stage, characterPreviewFactory, world, characterPreviewEventBus);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;
            leaving = false;
            viewInstance!.CloseButton.gameObject.SetActive(!inputData.IsStartup);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            avatarCts = avatarCts.SafeRestart();
            ShowAvatarAsync(avatarCts.Token).Forget();

            profileButtonPresenter.LoadProfile();

            placesCts = placesCts.SafeRestart();
            ShowLandingCardLoading();
            ShowLandingPlaceAsync(placesCts.Token).Forget();
            ShowRecentPlacesAsync(placesCts.Token).Forget();
            ShowRecommendedPlacesAsync(placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            ShowEventsAsync(eventsCts.Token).Forget();

            friendsCts = friendsCts.SafeRestart();
            friends?.Show(friendsCts.Token);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
            friends?.Hide();
            friendsCts.SafeCancelAndDispose();
            avatarPreview!.OnHide();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

            // At startup the lobby is the only way into the world, and a fullscreen panel opened from it replaces
            // it instead of closing it: the lobby has to come back, otherwise nothing is left on screen and the flow never resumes
            if (inputData.IsStartup && !leaving)
                mvcManager.OnViewClosed += ShowAgainWhenTheScreenIsFree;
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

            if (!inputData.StartupToken.IsCancellationRequested)
                mvcManager.ShowAndForget(LobbyController.IssueCommand(inputData));
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            closeIntent = new UniTaskCompletionSource();

            await closeIntent.Task.AttachExternalCancellation(ct);
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
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        /// <summary>
        ///     Fills the hero card with the destination the session lands in. The card is always filled: at startup its Jump in
        ///     is the only way out of the lobby, so when the Places API cannot describe the destination an offline stand-in still lets the user in.
        /// </summary>
        private async UniTaskVoid ShowLandingPlaceAsync(CancellationToken ct)
        {
            LandingDestination destination = ResolveLandingDestination();

            Result<PlacesData.PlaceInfo?> result = await (destination.WorldName != null
                    ? placesAPIService.GetWorldByNameAsync(destination.WorldName, ct)
                    : placesAPIService.GetPlaceAsync(destination.Parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            ShowLandingCard(result.Success && result.Value != null ? result.Value : destination.ToOfflinePlace(), ct);
        }

        /// <summary>
        ///     The launch settings fold app arguments, the saved home and the spawn feature flag into the start parcel and the bootstrap realm;
        ///     that pick is frozen the first time the lobby shows so the card reads the same for the whole session.
        ///     Only a destination that was the home keeps following the home, as the user may move it while playing.
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

        /// <summary>
        ///     Fills the "Jump back in" cards with the most recently visited places, one card per place.
        /// </summary>
        private async UniTaskVoid ShowRecentPlacesAsync(CancellationToken ct)
        {
            LobbyPlaceCardView[] cards = viewInstance!.RecentPlaceCards;

            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetRecentlyVisitedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            recentPlaces.Clear();

            if (result.Success)
            {
                IReadOnlyList<PlacesData.PlaceInfo> places = result.Value.Data;

                for (var i = 0; i < places.Count && i < cards.Length; i++)
                    recentPlaces.Add(places[i]);
            }

            for (var i = 0; i < cards.Length; i++)
            {
                bool shown = i < recentPlaces.Count;

                if (shown)
                    ShowPlaceCard(cards[i], recentPlaces[i], ct);

                cards[i].gameObject.SetActive(shown);
            }

            viewInstance.RecentPlacesSection.SetActive(recentPlaces.Count > 0);
        }

        /// <summary>
        ///     Fills the "Recommended places" carousel with the featured (highlighted) destinations, the ones the Places menu tags as Featured.
        /// </summary>
        private async UniTaskVoid ShowRecommendedPlacesAsync(CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetHighlightedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            recommendedPlaces.Clear();

            if (result.Success)
            {
                IReadOnlyList<PlacesData.PlaceInfo> places = result.Value.Data;

                for (var i = 0; i < places.Count; i++)
                    recommendedPlaces.Add(places[i]);
            }

            LobbyCarouselView carousel = viewInstance!.RecommendedPlaces;
            carousel.ShowCards(recommendedPlaces.Count);

            for (var i = 0; i < recommendedPlaces.Count; i++)
                ShowPlaceCard((LobbyPlaceCardView)carousel.Cards[i], recommendedPlaces[i], ct);

            viewInstance.RecommendedPlacesSection.SetActive(recommendedPlaces.Count > 0);
        }

        /// <summary>
        ///     Fills the "Events" carousels from a single fetch of the schedule: what is live right now first, then what comes next.
        ///     The live carousel is hidden while nothing is live, and the whole section while nothing is scheduled at all.
        /// </summary>
        private async UniTaskVoid ShowEventsAsync(CancellationToken ct)
        {
            Result<IReadOnlyList<EventDTO>> result = await eventsApiService.GetEventsAsync(ct, withConnectedUsers: true)
                                                                           .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested) return;

            liveEvents.Clear();
            upcomingEvents.Clear();

            if (result.Success)
            {
                IReadOnlyList<EventDTO> events = result.Value;

                for (var i = 0; i < events.Count; i++)
                    (events[i].live ? liveEvents : upcomingEvents).Add(events[i]);
            }

            // The schedule is not guaranteed to come sorted and can be long: keep only the next few
            upcomingEvents.Sort(BY_START_TIME);

            if (upcomingEvents.Count > MAX_UPCOMING_EVENTS)
                upcomingEvents.RemoveRange(MAX_UPCOMING_EVENTS, upcomingEvents.Count - MAX_UPCOMING_EVENTS);

            ShowLiveEventCards(viewInstance!.LiveEvents, liveEvents, ct);
            ShowUpcomingEventCards(viewInstance.UpcomingEvents, upcomingEvents);

            viewInstance.LiveEventsSection.SetActive(liveEvents.Count > 0);
            viewInstance.UpcomingEventsSection.SetActive(upcomingEvents.Count > 0);
            viewInstance.EventsSection.SetActive(liveEvents.Count > 0 || upcomingEvents.Count > 0);
        }

        private void ShowLandingCardLoading()
        {
            LobbyLandingCardView card = viewInstance!.LandingCard;
            shownLandingPlace = null;
            card.TitleText.text = string.Empty;
            card.CreatorText.text = string.Empty;
            card.OnlineCounter.SetActive(false);
            card.Thumbnail.IsLoading = true;
            card.JumpInButton.SetInteractable(false);
        }

        private void ShowLandingCard(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            LobbyLandingCardView card = viewInstance!.LandingCard;
            shownLandingPlace = place;
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;
            ShowOnlineCount(card.OnlineCounter, card.OnlineCountText, place);
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
            card.JumpInButton.SetInteractable(true);
        }

        private void ShowPlaceCard(LobbyPlaceCardView card, PlacesData.PlaceInfo place, CancellationToken ct)
        {
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;
            ShowOnlineCount(card.OnlineCounter, card.OnlineCountText, place);
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
        }

        // The addresses come only from the endpoints that resolve connected users; the aggregated count is the fallback
        private static void ShowOnlineCount(GameObject counter, TMP_Text countText, PlacesData.PlaceInfo place)
        {
            int online = place.connected_addresses?.Length ?? place.user_count;
            countText.text = online.ToString();
            counter.SetActive(true);
        }

        private void ShowLiveEventCards(LobbyCarouselView carousel, List<EventDTO> events, CancellationToken ct)
        {
            carousel.ShowCards(events.Count);

            for (var i = 0; i < events.Count; i++)
                ShowLiveEventCard((LobbyLiveEventCardView)carousel.Cards[i], events[i], ct);
        }

        private void ShowLiveEventCard(LobbyLiveEventCardView card, EventDTO @event, CancellationToken ct)
        {
            card.TitleText.text = @event.name;
            card.HostText.text = string.Format(EVENT_HOST_FORMAT, @event.user_name);

            int connectedUsers = @event.connected_addresses?.Length ?? 0;
            card.AttendeesGroup.SetActive(connectedUsers > 0);
            card.AttendeesText.text = connectedUsers.ToString();

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(@event.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
        }

        // The Explore card would print the start time of day; here how long until it starts reads better next to the live ones
        private void ShowUpcomingEventCards(LobbyEventRailView rail, List<EventDTO> events)
        {
            rail.ShowCards(events.Count);

            for (var i = 0; i < events.Count; i++)
            {
                EventCardView card = rail.Cards[i];
                card.Configure(events[i], thumbnailLoader);
                card.SetDateText(EventUtilities.GetEventStartsInText(events[i]));
            }
        }

        // Jump in is not wired: the card hides that button while the event is not live
        private void SubscribeToUpcomingCard(EventCardView card)
        {
            card.MainButtonClicked += OnUpcomingEventClicked;
            card.InterestedButtonClicked += OnUpcomingEventInterested;
            card.AddToCalendarButtonClicked += OnUpcomingEventAddToCalendar;
            card.EventShareButtonClicked += OnUpcomingEventShare;
            card.EventCopyLinkButtonClicked += OnUpcomingEventCopyLink;
        }

        private void UnsubscribeFromUpcomingCard(EventCardView card)
        {
            card.MainButtonClicked -= OnUpcomingEventClicked;
            card.InterestedButtonClicked -= OnUpcomingEventInterested;
            card.AddToCalendarButtonClicked -= OnUpcomingEventAddToCalendar;
            card.EventShareButtonClicked -= OnUpcomingEventShare;
            card.EventCopyLinkButtonClicked -= OnUpcomingEventCopyLink;
        }

        private void OnProfileUpdated(Profile profile)
        {
            ShowWelcome(profile);
            avatarPreview!.Refresh(profile.Avatar);
        }

        private void ShowWelcome(Profile? profile)
        {
            string? name = profile?.ValidatedName;
            viewInstance!.WelcomeText.text = string.IsNullOrEmpty(name) ? "WELCOME!" : $"WELCOME {name}!";
        }

        private void OnLandingJumpInClicked()
        {
            if (shownLandingPlace == null) return;

            // Before the world loads the card mirrors the destination the launch settings already picked, so there is nothing to reassign
            if (startParcel.IsConsumed())
                OnPlaceJumpIn(shownLandingPlace);
            else
                RequestClose();
        }

        private void OnRecentPlaceClicked(int index)
        {
            if (index < recentPlaces.Count)
                OnPlaceClicked(recentPlaces[index]);
        }

        private void OnRecommendedPlaceClicked(int index)
        {
            if (index < recommendedPlaces.Count)
                OnPlaceClicked(recommendedPlaces[index]);
        }

        private void OnRecentPlaceJumpIn(int index)
        {
            if (index < recentPlaces.Count)
                OnPlaceJumpIn(recentPlaces[index]);
        }

        private void OnRecommendedPlaceJumpIn(int index)
        {
            if (index < recommendedPlaces.Count)
                OnPlaceJumpIn(recommendedPlaces[index]);
        }

        private void OnLiveEventClicked(int index)
        {
            if (index < liveEvents.Count)
                OnEventClicked(liveEvents[index]);
        }

        // The card is handed over so that toggling Interested in the details also updates it
        private void OnUpcomingEventClicked(EventDTO @event, PlacesData.PlaceInfo? _, EventCardView card) =>
            OnEventClicked(@event, card);

        private void OnUpcomingEventInterested(EventDTO @event, EventCardView card) =>
            eventCardActions.SetEventAsInterestedAsync(@event, card, null, eventsCts!.Token).Forget();

        private void OnUpcomingEventAddToCalendar(EventDTO @event) =>
            eventCardActions.AddEventToCalendar(@event);

        private void OnUpcomingEventShare(EventDTO @event) =>
            eventCardActions.ShareEvent(@event);

        private void OnUpcomingEventCopyLink(EventDTO @event) =>
            eventCardActions.CopyEventLink(@event);

        // The backpack stacks on top of this panel as a popup and brings its own avatar preview, so the lobby keeps rendering behind it.
        // Saving in the backpack pushes a profile update, which is what re-dresses the lobby avatar through OnProfileUpdated
        private void OnAvatarClicked(PointerEventData _) =>
            mvcManager.ShowAndForget(BackpackModalController.IssueCommand(new BackpackModalParameter(BackpackSections.Avatar)));

        // The place details open in the same modal the Places menu uses; jumping in from there comes back through OnPlaceJumpIn
        private void OnPlaceClicked(PlacesData.PlaceInfo place) =>
            mvcManager.ShowAndForget(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(place, jumpInHandler: OnPlaceJumpIn)));

        private void OnPlaceJumpIn(PlacesData.PlaceInfo place) =>
            PickDestination(place.IsWorld ? WorldUrl(place.world_name) : null, place.base_position_processed, landOnParcel: false);

        // The event details open in the same modal the Explore menu uses; jumping in from there comes back through OnEventJumpIn
        private void OnEventClicked(EventDTO @event, EventCardView? card = null) =>
            mvcManager.ShowAndForget(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, placeData: null, card, OnEventJumpIn)));

        // Land on the exact parcel of the event rather than on the scene spawn point: the event may be held in a corner of a big scene
        private void OnEventJumpIn(IEventDTO @event) =>
            PickDestination(@event.World ? WorldUrl(@event.Server) : null, new Vector2Int(@event.X, @event.Y), landOnParcel: true);

        // Land next to the friend rather than on the scene spawn point
        private void OnFriendJoin(OnlineUserData friend) =>
            PickDestination(friend.worldName is { Length: > 0 } worldName ? WorldUrl(worldName) : null, friend.position.ToParcel(), landOnParcel: true);

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

        // Popups stack on top of this fullscreen panel; the MVC manager owns their closer, escape handling and teardown
        private void ShowProfileMenu() =>
            mvcManager.ShowAndForget(ProfileMenuController<LobbyPopupParameter>.IssueCommand(new LobbyPopupParameter()));

        private void ShowNotifications() =>
            mvcManager.ShowAndForget(NotificationsPanelController<LobbyPopupParameter>.IssueCommand(new LobbyPopupParameter()));

        private void RequestClose()
        {
            // At startup there is no close button: leaving means the user is on their way in, which releases the flow
            leaving = true;
            inputData.JumpedIn?.Invoke();

            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }

    /// <summary>
    ///     Where the session lands: a parcel of Genesis City, or a world (the parcel is then only a stand-in for offline display).
    /// </summary>
    internal readonly struct LandingDestination
    {
        private const string GENESIS_PLAZA_TITLE = "Genesis Plaza";

        public readonly Vector2Int Parcel;
        public readonly string? WorldName;

        public LandingDestination(Vector2Int parcel, string? worldName)
        {
            Parcel = parcel;
            WorldName = worldName;
        }

        public PlacesData.PlaceInfo ToOfflinePlace() =>
            new (Parcel)
            {
                title = WorldName ?? (Parcel == Vector2Int.zero ? GENESIS_PLAZA_TITLE : $"{Parcel.x},{Parcel.y}"),
                world_name = WorldName ?? string.Empty,
                base_position_processed = Parcel,
            };
    }

    /// <summary>
    ///     Input type of the lobby's own profile menu and notifications popups. The MVC manager keys controllers by view and
    ///     input type, so this keeps them registered next to the sidebar's instances of the same controllers.
    /// </summary>
    public readonly struct LobbyPopupParameter { }

    public readonly struct LobbyParameter
    {
        /// <summary>
        ///     True when the lobby gates the in-app initialization flow (first show of the session), false when the user opened it on demand in-world.
        ///     At startup Jump in is the only way out, so no close button is offered.
        /// </summary>
        public readonly bool IsStartup;

        /// <summary>
        ///     Releases the startup flow, which stays parked until the user is on their way in. Being taken off the screen is not
        ///     enough: a fullscreen panel opened from the lobby (the backpack) replaces it and the lobby comes back when it closes.
        /// </summary>
        public readonly Action? JumpedIn;

        /// <summary>
        ///     Cancelled when a Logout takes the startup flow over: the authentication screen owns the screen from then on and
        ///     the lobby must not show itself again.
        /// </summary>
        public readonly CancellationToken StartupToken;

        public LobbyParameter(bool isStartup, Action? jumpedIn = null, CancellationToken startupToken = default)
        {
            IsStartup = isStartup;
            JumpedIn = jumpedIn;
            StartupToken = startupToken;
        }
    }
}
