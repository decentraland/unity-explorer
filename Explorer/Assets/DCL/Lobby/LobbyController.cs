using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Notifications.NotificationsMenu;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private const string GENESIS_PLAZA_TITLE = "Genesis Plaza";
        private const string EVENT_HOST_FORMAT = "By {0}";
        private const string EVENT_STARTING_NOW = "Starting now";
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
        private readonly World world;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IHomePlaceSource homePlace;
        private readonly HttpEventsApiService eventsApiService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly StartParcel startParcel;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly List<PlacesData.PlaceInfo> recentPlaces = new ();
        private readonly List<PlacesData.PlaceInfo> recommendedPlaces = new ();
        private readonly List<EventDTO> liveEvents = new ();
        private readonly List<EventDTO> upcomingEvents = new ();

        private LobbyCharacterPreviewController? avatarPreview;
        private PlacesData.PlaceInfo? shownHomePlace;
        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
        private CancellationTokenSource? eventsCts;
        private CancellationTokenSource? jumpInCts;
        private UniTaskCompletionSource? closeIntent;

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
            World world,
            IPlacesAPIService placesAPIService,
            IHomePlaceSource homePlace,
            HttpEventsApiService eventsApiService,
            IRealmNavigator realmNavigator,
            IDecentralandUrlsSource decentralandUrlsSource,
            StartParcel startParcel,
            ThumbnailLoader thumbnailLoader,
            SidebarProfileButtonPresenter profileButtonPresenter) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.avatarSettings = avatarSettings;
            this.world = world;
            this.placesAPIService = placesAPIService;
            this.homePlace = homePlace;
            this.eventsApiService = eventsApiService;
            this.realmNavigator = realmNavigator;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.startParcel = startParcel;
            this.thumbnailLoader = thumbnailLoader;
            this.profileButtonPresenter = profileButtonPresenter;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
            {
                viewInstance.HomeCard.JumpInButton.Button.onClick.RemoveListener(OnHomeJumpInClicked);
                viewInstance.CloseButton.onClick.RemoveListener(RequestClose);
                viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent -= OnAvatarClicked;
                viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.RemoveListener(ShowProfileMenu);
                viewInstance.NotificationsButton.onClick.RemoveListener(ShowNotifications);

                foreach (LobbyPlaceCardView card in viewInstance.RecentPlaceCards)
                    card.Button.onClick.RemoveAllListeners();

                viewInstance.RecommendedPlaces.CardClicked = null;
                viewInstance.LiveEvents.CardClicked = null;
                viewInstance.UpcomingEvents.CardClicked = null;
            }

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
            jumpInCts.SafeCancelAndDispose();
            avatarPreview?.Dispose();
            closeIntent?.TrySetCanceled();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.HomeCard.JumpInButton.Button.onClick.AddListener(OnHomeJumpInClicked);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent += OnAvatarClicked;
            viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.AddListener(ShowProfileMenu);
            viewInstance.NotificationsButton.onClick.AddListener(ShowNotifications);

            LobbyPlaceCardView[] recentCards = viewInstance.RecentPlaceCards;

            for (var i = 0; i < recentCards.Length; i++)
            {
                int index = i;
                recentCards[i].Button.onClick.AddListener(() => OnRecentPlaceClicked(index));
            }

            viewInstance.RecommendedPlaces.CardClicked = OnRecommendedPlaceClicked;
            viewInstance.LiveEvents.CardClicked = OnLiveEventClicked;
            viewInstance.UpcomingEvents.CardClicked = OnUpcomingEventClicked;

            viewInstance.RecentPlacesSection.SetActive(false);
            viewInstance.RecommendedPlacesSection.SetActive(false);
            viewInstance.EventsSection.SetActive(false);

            avatarPreview = new LobbyCharacterPreviewController(viewInstance.CharacterPreviewView, avatarSettings, characterPreviewFactory, world, characterPreviewEventBus);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
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
            ShowHomeCardLoading();
            ShowHomePlaceAsync(placesCts.Token).Forget();
            ShowRecentPlacesAsync(placesCts.Token).Forget();
            ShowRecommendedPlacesAsync(placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            ShowEventsAsync(eventsCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
            avatarPreview!.OnHide();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
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
        ///     Fills the hero card with the place set as home, or Genesis Plaza when there is none.
        ///     The card is always filled: at startup its Jump in is the only way out of the lobby, so when the Places API
        ///     cannot be reached an offline Genesis Plaza still lets the user in.
        /// </summary>
        private async UniTaskVoid ShowHomePlaceAsync(CancellationToken ct)
        {
            PlacesData.PlaceInfo? place = await ResolveHomePlaceAsync(ct);

            if (ct.IsCancellationRequested) return;

            place ??= new PlacesData.PlaceInfo(Vector2Int.zero) { title = GENESIS_PLAZA_TITLE };

            ShowHomeCard(place, ct);
        }

        private async UniTask<PlacesData.PlaceInfo?> ResolveHomePlaceAsync(CancellationToken ct)
        {
            string? homeWorld = homePlace.IsWorldHome ? homePlace.CurrentHomeWorldName : null;
            Vector2Int homeParcel = homePlace.CurrentHomeCoordinates ?? Vector2Int.zero;

            Result<PlacesData.PlaceInfo?> result = await (homeWorld != null
                    ? placesAPIService.GetWorldByNameAsync(homeWorld, ct)
                    : placesAPIService.GetPlaceAsync(homeParcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return null;

            if (result.Success && result.Value != null)
                return result.Value;

            // A home that no longer resolves (deleted world, parcel with no place) yields to Genesis Plaza
            if (homeWorld == null && homeParcel == Vector2Int.zero)
                return null;

            result = await placesAPIService.GetPlaceAsync(Vector2Int.zero, ct).SuppressToResultAsync(ReportCategory.PLACES);

            return result.Success ? result.Value : null;
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

            ShowEventCards(viewInstance!.LiveEvents, liveEvents, ct);
            ShowEventCards(viewInstance.UpcomingEvents, upcomingEvents, ct);

            viewInstance.LiveEventsSection.SetActive(liveEvents.Count > 0);
            viewInstance.UpcomingEventsSection.SetActive(upcomingEvents.Count > 0);
            viewInstance.EventsSection.SetActive(liveEvents.Count > 0 || upcomingEvents.Count > 0);
        }

        private void ShowHomeCardLoading()
        {
            LobbyHomeCardView card = viewInstance!.HomeCard;
            shownHomePlace = null;
            card.TitleText.text = string.Empty;
            card.CreatorText.text = string.Empty;
            card.OnlineCounter.SetActive(false);
            card.Thumbnail.IsLoading = true;
            card.JumpInButton.SetInteractable(false);
        }

        private void ShowHomeCard(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            LobbyHomeCardView card = viewInstance!.HomeCard;
            shownHomePlace = place;
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;

            int online = place.connected_addresses?.Length ?? place.user_count;
            card.OnlineCountText.text = online.ToString();
            card.OnlineCounter.SetActive(online > 0);

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
            card.JumpInButton.SetInteractable(true);
        }

        private void ShowPlaceCard(LobbyPlaceCardView card, PlacesData.PlaceInfo place, CancellationToken ct)
        {
            card.TitleText.text = place.title;
            card.CreatorText.text = place.contact_name;
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
        }

        private void ShowEventCards(LobbyCarouselView carousel, List<EventDTO> events, CancellationToken ct)
        {
            carousel.ShowCards(events.Count);

            for (var i = 0; i < events.Count; i++)
                ShowEventCard((LobbyEventCardView)carousel.Cards[i], events[i], ct);
        }

        /// <summary>
        ///     A live event shows how many people are connected; one yet to start shows how long until it does.
        /// </summary>
        private void ShowEventCard(LobbyEventCardView card, EventDTO @event, CancellationToken ct)
        {
            card.TitleText.text = @event.name;
            card.HostText.text = string.Format(EVENT_HOST_FORMAT, @event.user_name);

            int connectedUsers = @event.connected_addresses?.Length ?? 0;
            card.LiveBadge.SetActive(@event.live);
            card.AttendeesGroup.SetActive(@event.live && connectedUsers > 0);
            card.AttendeesText.text = connectedUsers.ToString();

            card.ScheduleText.gameObject.SetActive(!@event.live);
            card.ScheduleText.text = @event.live ? string.Empty : StartsIn(@event.NextStartAtProcessed - DateTime.UtcNow);

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(@event.image, card.Thumbnail, card.DefaultThumbnail, ct, true).Forget();
        }

        private static string StartsIn(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return EVENT_STARTING_NOW;
            if (remaining.TotalHours < 1) return $"In {Mathf.Max(1, Mathf.RoundToInt((float)remaining.TotalMinutes))} min";
            if (remaining.TotalDays < 1) return InUnits(Mathf.RoundToInt((float)remaining.TotalHours), "hour");

            return InUnits(Mathf.RoundToInt((float)remaining.TotalDays), "day");
        }

        private static string InUnits(int amount, string unit) =>
            amount == 1 ? $"In 1 {unit}" : $"In {amount} {unit}s";

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

        private void OnHomeJumpInClicked()
        {
            if (shownHomePlace != null)
                OnPlaceClicked(shownHomePlace);
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

        private void OnLiveEventClicked(int index)
        {
            if (index < liveEvents.Count)
                OnEventClicked(liveEvents[index]);
        }

        private void OnUpcomingEventClicked(int index)
        {
            if (index < upcomingEvents.Count)
                OnEventClicked(upcomingEvents[index]);
        }

        // Until the backpack gets its own modal the Explore panel takes over; being fullscreen it also closes this panel.
        private void OnAvatarClicked(PointerEventData _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)));

        private void OnPlaceClicked(PlacesData.PlaceInfo place) =>
            PickDestination(place.IsWorld ? WorldUrl(place.world_name) : null, place.base_position_processed, landOnParcel: false);

        // The event details open in the same modal the Explore menu uses; jumping in from there comes back through OnEventJumpIn
        private void OnEventClicked(EventDTO @event) =>
            mvcManager.ShowAndForget(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(@event, placeData: null, jumpInHandler: OnEventJumpIn)));

        // Land on the exact parcel of the event rather than on the scene spawn point: the event may be held in a corner of a big scene
        private void OnEventJumpIn(IEventDTO @event) =>
            PickDestination(@event.World ? WorldUrl(@event.Server) : null, new Vector2Int(@event.X, @event.Y), landOnParcel: true);

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
            closeIntent?.TrySetResult();
            closeIntent = null;
        }
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

        public LobbyParameter(bool isStartup)
        {
            IsStartup = isStartup;
        }
    }
}
