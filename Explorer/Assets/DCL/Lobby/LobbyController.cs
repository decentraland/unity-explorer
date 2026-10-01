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
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world loads and on demand in-world; it only reports the close intent, what follows is up to the caller.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView, LobbyParameter>, ILobbyController
    {
        private const string EVENT_HOST_FORMAT = "By {0}";
        private const string WELCOME_FALLBACK = "Welcome!";
        private const string WELCOME_FORMAT = "Welcome {0}!";
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

        // The destination the shown landing place describes, meaningful only while that place is set
        private LandingDestination shownLandingDestination;
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

        public event Action<bool>? Opened;
        public event Action? Closed;
        public event Action<PlacesData.PlaceInfo, LobbySection>? PlaceOpened;
        public event Action<PlacesData.PlaceInfo, LobbySection>? PlaceJumpedIn;
        public event Action<IEventDTO, LobbySection>? EventOpened;
        public event Action<IEventDTO, LobbySection>? EventJumpedIn;
        public event Action<string, Vector2Int>? FriendJoined;

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
                viewInstance.LandingCard.Button.onClick.RemoveListener(OnLandingCardClicked);
                viewInstance.LandingCard.JumpInButton.Button.onClick.RemoveListener(OnLandingJumpInClicked);
                viewInstance.CloseButton.onClick.RemoveListener(RequestClose);
                viewInstance.AvatarButton.onClick.RemoveListener(OnAvatarClicked);
                viewInstance.AvatarButton.OnButtonHover -= OnAvatarHovered;
                viewInstance.AvatarButton.OnButtonUnhover -= OnAvatarUnhovered;
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
            startParcel.JumpInRequestRaised -= RequestClose;

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
            viewInstance!.LandingCard.Button.onClick.AddListener(OnLandingCardClicked);
            viewInstance.LandingCard.JumpInButton.Button.onClick.AddListener(OnLandingJumpInClicked);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.AvatarButton.onClick.AddListener(OnAvatarClicked);
            viewInstance.AvatarButton.OnButtonHover += OnAvatarHovered;
            viewInstance.AvatarButton.OnButtonUnhover += OnAvatarUnhovered;
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
            LandingDestination destination = ResolveLandingDestination();

            // A card already showing the destination keeps it up while its details are fetched again, instead of going dark for the round trip
            if (shownLandingPlace == null || !shownLandingDestination.Equals(destination))
                ShowLandingCardLoading();

            ShowLandingPlaceAsync(destination, placesCts.Token).Forget();
            ShowRecentPlacesAsync(placesCts.Token).Forget();
            ShowRecommendedPlacesAsync(placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            ShowEventsAsync(eventsCts.Token).Forget();

            friendsCts = friendsCts.SafeRestart();
            friends?.Show(friendsCts.Token);

            if (inputData.IsStartup)
                startParcel.JumpInRequestRaised += RequestClose;

            Opened?.Invoke(inputData.IsStartup);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            if (inputData.IsStartup)
                startParcel.JumpInRequestRaised -= RequestClose;

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            // Nulled so a late card callback finds no source instead of reading the token of a disposed one, which throws
            avatarCts.SafeCancelAndDispose();
            avatarCts = null;
            placesCts.SafeCancelAndDispose();
            placesCts = null;
            eventsCts.SafeCancelAndDispose();
            eventsCts = null;
            friends?.Hide();
            friendsCts.SafeCancelAndDispose();
            friendsCts = null;
            avatarPreview!.OnHide();
            viewInstance!.AvatarButton.gameObject.SetActive(false);

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);

            // A fullscreen panel opened from the startup lobby replaces it instead of closing it, so the lobby has to come back or the flow never resumes
            if (inputData.IsStartup && !leaving)
                mvcManager.OnViewClosed += ShowAgainWhenTheScreenIsFree;

            Closed?.Invoke();
        }

        /// <summary>
        ///     Shows the startup lobby again once the panel that replaced it is gone; a cancelled startup token means a Logout owns the screen instead.
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
                mvcManager.ShowAndForget(LobbyController.IssueCommand(inputData));
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

                // The hit area only makes sense over a figure, so it comes up with the avatar and goes down with the panel
                viewInstance!.AvatarButton.gameObject.SetActive(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        /// <summary>
        ///     At startup the hero card's Jump in is the only way out, so an offline stand-in fills it when the Places API cannot describe the destination.
        /// </summary>
        private async UniTaskVoid ShowLandingPlaceAsync(LandingDestination destination, CancellationToken ct)
        {
            Result<PlacesData.PlaceInfo?> result = await (destination.WorldName != null
                    ? placesAPIService.GetWorldByNameAsync(destination.WorldName, ct)
                    : placesAPIService.GetPlaceAsync(destination.Parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

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

        private async UniTaskVoid ShowRecentPlacesAsync(CancellationToken ct)
        {
            LobbyPlaceCardView[] cards = viewInstance!.RecentPlaceCards;

            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetRecentlyVisitedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            // A failed fetch keeps the places of the last one that succeeded, so the row is only hidden while none have ever arrived
            if (result.Success)
            {
                recentPlaces.Clear();
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

        private async UniTaskVoid ShowRecommendedPlacesAsync(CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetHighlightedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                recommendedPlaces.Clear();
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
        ///     A single schedule fetch feeds both the live and the upcoming carousels; a failed fetch keeps the events of the last one
        ///     that succeeded, so the rows are only hidden while none have ever arrived.
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
            card.Button.interactable = false;
        }

        // An offline stand-in carries nothing but the destination itself, so that card only offers Jump in
        private void ShowLandingCard(LandingDestination destination, PlacesData.PlaceInfo place, bool hasDetails, CancellationToken ct)
        {
            LobbyLandingCardView card = viewInstance!.LandingCard;
            shownLandingPlace = place;
            shownLandingDestination = destination;
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;
            ShowOnlineCount(card.OnlineCounter, card.OnlineCountText, place);
            ShowThumbnail(card.Thumbnail, place.image, card.DefaultThumbnail, ct);
            card.JumpInButton.SetInteractable(true);
            card.Button.interactable = hasDetails;
        }

        private void ShowPlaceCard(LobbyPlaceCardView card, PlacesData.PlaceInfo place, CancellationToken ct)
        {
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;
            ShowOnlineCount(card.OnlineCounter, card.OnlineCountText, place);
            ShowThumbnail(card.Thumbnail, place.image, card.DefaultThumbnail, ct);
        }

        /// <summary>
        ///     A picture already cached goes up at once, so a card does not pass through its loading look for an image it can show right away.
        /// </summary>
        private void ShowThumbnail(ImageView thumbnail, string? url, Sprite? defaultThumbnail, CancellationToken ct)
        {
            Sprite? cached = string.IsNullOrEmpty(url) ? null : thumbnailLoader.Cache!.GetCachedSprite(url);

            if (cached == null)
            {
                thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(url, thumbnail, defaultThumbnail, ct, true).Forget();
                return;
            }

            thumbnail.SetImage(cached, true);
            thumbnail.ImageColor = Color.white;
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
            card.AttendeesText.text = connectedUsers.ToString();
            card.AttendeesGroup.SetActive(true);

            ShowThumbnail(card.Thumbnail, @event.image, card.DefaultThumbnail, ct);
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
            viewInstance!.WelcomeText.text = string.IsNullOrEmpty(name) ? WELCOME_FALLBACK : string.Format(WELCOME_FORMAT, name);
        }

        private void OnLandingJumpInClicked()
        {
            if (shownLandingPlace == null) return;

            // Before the world loads the card mirrors the destination the launch settings already picked, so there is nothing to reassign
            if (startParcel.IsConsumed())
                OnPlaceJumpIn(shownLandingPlace, LobbySection.Landing);
            else
            {
                PlaceJumpedIn?.Invoke(shownLandingPlace, LobbySection.Landing);
                RequestClose();
            }
        }

        private void OnLandingCardClicked()
        {
            if (shownLandingPlace == null) return;

            OnPlaceClicked(shownLandingPlace, LobbySection.Landing);
        }

        private void OnRecentPlaceClicked(int index)
        {
            if (index < recentPlaces.Count)
                OnPlaceClicked(recentPlaces[index], LobbySection.Recent);
        }

        private void OnRecommendedPlaceClicked(int index)
        {
            if (index < recommendedPlaces.Count)
                OnPlaceClicked(recommendedPlaces[index], LobbySection.Recommended);
        }

        private void OnRecentPlaceJumpIn(int index)
        {
            if (index < recentPlaces.Count)
                OnPlaceJumpIn(recentPlaces[index], LobbySection.Recent);
        }

        private void OnRecommendedPlaceJumpIn(int index)
        {
            if (index < recommendedPlaces.Count)
                OnPlaceJumpIn(recommendedPlaces[index], LobbySection.Recommended);
        }

        private void OnLiveEventClicked(int index)
        {
            if (index < liveEvents.Count)
                OnEventClicked(liveEvents[index], LobbySection.LiveEvents);
        }

        // The card is handed over so that toggling Interested in the details also updates it
        private void OnUpcomingEventClicked(EventDTO @event, PlacesData.PlaceInfo? _, EventCardView card) =>
            OnEventClicked(@event, LobbySection.UpcomingEvents, card);

        private void OnUpcomingEventInterested(EventDTO @event, EventCardView card)
        {
            if (eventsCts == null) return;

            eventCardActions.SetEventAsInterestedAsync(@event, card, null, eventsCts.Token).Forget();
        }

        private void OnUpcomingEventAddToCalendar(EventDTO @event) =>
            eventCardActions.AddEventToCalendar(@event);

        private void OnUpcomingEventShare(EventDTO @event) =>
            eventCardActions.ShareEvent(@event);

        private void OnUpcomingEventCopyLink(EventDTO @event) =>
            eventCardActions.CopyEventLink(@event);

        // The backpack stacks on this panel as a popup; saving there pushes the profile update that re-dresses the lobby avatar
        private void OnAvatarClicked() =>
            mvcManager.ShowAndForget(BackpackModalController.IssueCommand(new BackpackModalParameter(BackpackSections.Avatar)));

        private void OnAvatarHovered() =>
            avatarPreview!.SetHovered(true);

        private void OnAvatarUnhovered() =>
            avatarPreview!.SetHovered(false);

        // The section travels with the handler so a Jump in from the details is attributed to the row the card sits in
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

        private void OnEventClicked(EventDTO @event, LobbySection section, EventCardView? card = null)
        {
            EventOpened?.Invoke(@event, section);
            mvcManager.ShowAndForget(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, placeData: null, card, jumped => OnEventJumpIn(jumped, section))));
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
        ///     Before the world loads the pick becomes the startup destination, in-world it teleports right away; either way the lobby closes.
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
    internal readonly struct LandingDestination : IEquatable<LandingDestination>
    {
        private const string GENESIS_PLAZA_TITLE = "Genesis Plaza";

        public readonly Vector2Int Parcel;
        public readonly string? WorldName;

        public LandingDestination(Vector2Int parcel, string? worldName)
        {
            Parcel = parcel;
            WorldName = worldName;
        }

        public bool Equals(LandingDestination other) =>
            Parcel == other.Parcel && WorldName == other.WorldName;

        public override bool Equals(object? obj) =>
            obj is LandingDestination other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Parcel, WorldName);

        public PlacesData.PlaceInfo ToOfflinePlace() =>
            new (Parcel)
            {
                title = WorldName ?? (Parcel == Vector2Int.zero ? GENESIS_PLAZA_TITLE : $"{Parcel.x},{Parcel.y}"),
                world_name = WorldName ?? string.Empty,
                base_position_processed = Parcel,
            };
    }

    /// <summary>
    ///     The MVC manager keys controllers by view and input type, so this distinct type registers the lobby's popups next to the sidebar's.
    /// </summary>
    public readonly struct LobbyPopupParameter { }

    public readonly struct LobbyParameter
    {
        /// <summary>
        ///     True when the lobby gates the startup flow, where Jump in is the only way out and no close button is offered.
        /// </summary>
        public readonly bool IsStartup;

        /// <summary>
        ///     Releases the startup flow; leaving the screen is not enough, as a fullscreen panel opened from the lobby replaces it and it comes back.
        /// </summary>
        public readonly Action? JumpedIn;

        /// <summary>
        ///     Cancelled when a Logout takes the startup flow over, so the lobby must not show itself again.
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
