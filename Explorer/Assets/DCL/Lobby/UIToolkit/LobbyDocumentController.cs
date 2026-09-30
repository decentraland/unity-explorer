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
    ///     UI Toolkit rebuild of <see cref="LobbyController" />: the fullscreen panel shown before the world starts loading and,
    ///     later, on demand during gameplay. The player's avatar stands on the stage behind everything else, drawn by the uGUI
    ///     preview the view carries, and clicking it opens the backpack. It greets the user over the hero card of the place the session
    ///     lands in, whose Jump in is the only way out at startup. It lists the recently visited and the featured places;
    ///     a card opens the place details and Jump in leaves for the place. It lists the live events and the next upcoming ones;
    ///     a card opens the event details, from where Jump in leaves for the event's parcel. Its top bar shows the credits and the
    ///     profile of the user, whose popups stack on top of it. It only reports the close intent; what happens next is up to the caller.
    /// </summary>
    public class LobbyDocumentController : ControllerBase<LobbyDocumentView, LobbyParameter>
    {
        private const string LIVE_EVENT_HOST_FORMAT = "By {0}";
        private const string UPCOMING_EVENT_HOST_FORMAT = "By <b>{0}</b>";
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
        private readonly List<PlacesData.PlaceInfo> recentPlaces = new ();
        private readonly List<PlacesData.PlaceInfo> featuredPlaces = new ();
        private readonly List<EventDTO> liveEvents = new ();
        private readonly List<EventDTO> upcomingEvents = new ();

        // Stand-ins shown in the friends row until the friends logic is ported, so the row can be laid out with cards in it
        private static readonly PlaceholderFriend[] PLACEHOLDER_FRIENDS =
        {
            new ("Amy", "#1a2b", OnlineStatus.Online, "Genesis Plaza", canJoin: true, isVerified: false),
            new ("Bob", string.Empty, OnlineStatus.Online, "Lobby", canJoin: false, isVerified: true),
            new ("Carla", "#3c4d", OnlineStatus.Away, "Locating…", canJoin: false, isVerified: false),
            new ("Dan", string.Empty, OnlineStatus.Online, "myworld.dcl.eth", canJoin: true, isVerified: true),
            new ("Eve", "#5e6f", OnlineStatus.Online, "12,-34", canJoin: true, isVerified: false),
        };

        // The rails need the card template of the view, which exists only from the first show on
        private LobbyPlacesRail? recentPlacesRail;
        private LobbyPlacesRail? featuredPlacesRail;
        private LobbyFriendsRail? friendsRail;
        private LobbyLiveEventsRail? liveEventsRail;
        private LobbyUpcomingEventsRail? upcomingEventsRail;
        private LobbyCharacterPreviewController? avatarPreview;

        // Null while the landing place loads, when the card takes no click; without details the card only offers Jump in
        private PlacesData.PlaceInfo? landingPlace;
        private bool landingPlaceHasDetails;
        private LandingDestination? startupDestination;

        // One menu serves every Share button; the event it acts on is the one whose button opened it
        private GenericContextMenu? shareMenu;
        private EventDTO sharedEvent;
        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
        private CancellationTokenSource? eventsCts;
        private CancellationTokenSource? jumpInCts;
        private UniTaskCompletionSource? closeIntent;
        private bool leaving;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // At startup the lobby is the only way into the world, so it cannot be dismissed before a destination is picked
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;

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
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
                viewInstance.Profile.Clicked = null;

            mvcManager.OnViewClosed -= ShowAgainWhenTheScreenIsFree;

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
            eventsCts.SafeCancelAndDispose();
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

        // The hierarchy is rebuilt on every show, so the rows are handed their section here rather than once at instantiation
        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            // The widgets outlive the hierarchy, so their presenters stay bound; the buttons are rebuilt with it and rewired here
            viewInstance!.AttachTopBarWidgets();
            viewInstance.Profile.Clicked = ShowProfileMenu;
            viewInstance.NotificationsButton.clicked += ShowNotifications;
            viewInstance.CloseButton.clicked += RequestClose;
            viewInstance.CloseButton.SetDisplayed(!inputData.IsStartup);
            profileButtonPresenter.LoadProfile();

            VisualElement avatarHitArea = viewInstance.AvatarHitArea;
            avatarHitArea.AddManipulator(new Clickable(OnAvatarClicked));
            avatarHitArea.RegisterCallback<PointerEnterEvent>(OnAvatarPointerEnter);
            avatarHitArea.RegisterCallback<PointerLeaveEvent>(OnAvatarPointerLeave);

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
            avatarCts = avatarCts.SafeRestart();
            ShowAvatarAsync(avatarCts.Token).Forget();

            LobbyLandingCardElement landingCard = viewInstance.LandingCard;
            landingCard.Clicked = OnLandingCardClicked;
            landingCard.JumpInClicked = OnLandingJumpInClicked;

            recentPlacesRail ??= CreatePlacesRail(recentPlaces);
            featuredPlacesRail ??= CreatePlacesRail(featuredPlaces);
            recentPlacesRail.Show(viewInstance.RecentPlaces);
            featuredPlacesRail.Show(viewInstance.FeaturedPlaces);

            friendsRail ??= new LobbyFriendsRail(viewInstance.FriendCardTemplate);
            friendsRail.Show(viewInstance.Friends);
            ShowPlaceholderFriends(friendsRail);

            liveEventsRail ??= new LobbyLiveEventsRail(viewInstance.LiveEventCardTemplate) { CardClicked = OnLiveEventClicked };
            upcomingEventsRail ??= CreateUpcomingEventsRail();
            liveEventsRail.Show(viewInstance.LiveEvents);
            upcomingEventsRail.Show(viewInstance.UpcomingEvents);

            placesCts = placesCts.SafeRestart();
            ShowLandingCardLoading(landingCard);
            ShowLandingPlaceAsync(placesCts.Token).Forget();
            ShowPlacesAsync(recentPlacesRail, recentPlaces, placesAPIService.GetRecentlyVisitedDestinationsAsync(placesCts.Token), MAX_RECENT_PLACES, placesCts.Token).Forget();
            ShowPlacesAsync(featuredPlacesRail, featuredPlaces, placesAPIService.GetHighlightedDestinationsAsync(placesCts.Token), int.MaxValue, placesCts.Token).Forget();

            eventsCts = eventsCts.SafeRestart();
            ShowEventsAsync(eventsCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            // Nulled so a late callback finds no source instead of reading the token of a disposed one, which throws
            avatarCts.SafeCancelAndDispose();
            avatarCts = null;
            placesCts.SafeCancelAndDispose();
            placesCts = null;
            eventsCts.SafeCancelAndDispose();
            eventsCts = null;

            // The hit area goes down with the hierarchy; the preview and its stage are released here since they outlive it
            avatarPreview!.OnHide();

            recentPlacesRail?.Hide();
            featuredPlacesRail?.Hide();
            friendsRail?.Hide();
            liveEventsRail?.Hide();
            upcomingEventsRail?.Hide();

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
                mvcManager.ShowAndForget(IssueCommand(inputData));
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

                // The hit area only makes sense over a figure, so it comes up with the avatar and goes down with the hierarchy
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

        private void ShowLandingCardLoading(LobbyLandingCardElement card)
        {
            landingPlace = null;
            landingPlaceHasDetails = false;
            card.Title = string.Empty;
            card.Creator = string.Empty;
            card.HasOnlineCount = false;
            card.CanJumpIn = false;
            card.Thumbnail = null;
            card.IsLoading = true;
        }

        /// <summary>
        ///     At startup the hero card's Jump in is the only way out, so an offline stand-in fills it when the Places API cannot describe the destination.
        /// </summary>
        private async UniTaskVoid ShowLandingPlaceAsync(CancellationToken ct)
        {
            LandingDestination destination = ResolveLandingDestination();

            Result<PlacesData.PlaceInfo?> result = await (destination.WorldName != null
                    ? placesAPIService.GetWorldByNameAsync(destination.WorldName, ct)
                    : placesAPIService.GetPlaceAsync(destination.Parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            PlacesData.PlaceInfo? place = result.Success ? result.Value : null;
            ShowLandingCard(place ?? destination.ToOfflinePlace(), hasDetails: place != null, ct);
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
        private void ShowLandingCard(PlacesData.PlaceInfo place, bool hasDetails, CancellationToken ct)
        {
            landingPlace = place;
            landingPlaceHasDetails = hasDetails;

            LobbyLandingCardElement card = viewInstance!.LandingCard;
            card.Title = place.title;
            card.Creator = place.contact_name;

            // The addresses come only from the endpoints that resolve connected users; the aggregated count is the fallback
            card.OnlineCount = place.connected_addresses?.Length ?? place.user_count;
            card.HasOnlineCount = true;
            card.CanJumpIn = true;

            LoadThumbnailAsync(card, place.image, ReportCategory.PLACES, ct).Forget();
        }

        private void OnLandingCardClicked()
        {
            if (landingPlace != null && landingPlaceHasDetails)
                OnPlaceClicked(landingPlace);
        }

        private void OnLandingJumpInClicked()
        {
            if (landingPlace == null) return;

            // Before the world loads the card mirrors the destination the launch settings already picked, so there is nothing to reassign
            if (startParcel.IsConsumed())
                OnPlaceJumpIn(landingPlace);
            else
                RequestClose();
        }

        private void OnAvatarClicked() =>
            mvcManager.ShowAndForget(BackpackModalController.IssueCommand(new BackpackModalParameter(BackpackSections.Avatar)));

        private void OnAvatarPointerEnter(PointerEnterEvent evt) =>
            avatarPreview!.SetHovered(true);

        private void OnAvatarPointerLeave(PointerLeaveEvent evt) =>
            avatarPreview!.SetHovered(false);

        private LobbyPlacesRail CreatePlacesRail(List<PlacesData.PlaceInfo> places) =>
            new (viewInstance!.PlaceCardTemplate)
            {
                CardClicked = index => OnPlaceClicked(places[index]),
                CardJumpInClicked = index => OnPlaceJumpIn(places[index]),
            };

        /// <summary>
        ///     Fills a row with the first <paramref name="maxCount" /> places <paramref name="fetch" /> resolves to, one card per place;
        ///     a failed fetch leaves the row hidden.
        /// </summary>
        private async UniTaskVoid ShowPlacesAsync(LobbyPlacesRail rail, List<PlacesData.PlaceInfo> places, UniTask<PlacesData.IPlacesAPIResponse> fetch, int maxCount, CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await fetch.SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            places.Clear();

            if (result.Success)
            {
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

        private async UniTaskVoid LoadThumbnailAsync(ILobbyThumbnailCard card, string? url, string reportCategory, CancellationToken ct)
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

            card.IsLoading = false;
            card.Thumbnail = sprite;
        }

        private LobbyUpcomingEventsRail CreateUpcomingEventsRail() =>
            new (viewInstance!.UpcomingEventCardTemplate)
            {
                CardClicked = OnUpcomingEventClicked,
                CardAddToCalendarClicked = OnUpcomingEventAddToCalendar,
                CardShareClicked = OnUpcomingEventShare,
            };

        /// <summary>
        ///     A single schedule fetch feeds both event rows; a failed fetch leaves them hidden.
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

        // The Explore card would print the start time of day; here how long until it starts reads better next to the live ones
        private void ShowUpcomingEventCard(LobbyUpcomingEventCardElement card, EventDTO @event, CancellationToken ct)
        {
            card.Title = @event.name;
            card.Host = string.Format(UPCOMING_EVENT_HOST_FORMAT, @event.user_name);
            card.StartsIn = EventUtilities.GetEventStartsInText(@event);
            card.IsInterested = @event.attending;

            LoadThumbnailAsync(card, @event.image, ReportCategory.EVENTS, ct).Forget();
        }

        private static void ShowPlaceholderFriends(LobbyFriendsRail rail)
        {
            rail.SetCount(PLACEHOLDER_FRIENDS.Length);

            for (var i = 0; i < PLACEHOLDER_FRIENDS.Length; i++)
            {
                PlaceholderFriend friend = PLACEHOLDER_FRIENDS[i];
                LobbyFriendCardElement card = rail.Cards[i];
                card.UserName = friend.Name;
                card.WalletTag = friend.WalletTag;
                card.OnlineStatus = friend.Status;
                card.Location = friend.Location;
                card.CanJoin = friend.CanJoin;
                card.IsVerified = friend.IsVerified;
            }
        }

        // The details open in the same modal the Explore menu uses; jumping in from there comes back through OnPlaceJumpIn
        private void OnPlaceClicked(PlacesData.PlaceInfo place) =>
            mvcManager.ShowAndForget(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(place, jumpInHandler: OnPlaceJumpIn)));

        private void OnPlaceJumpIn(PlacesData.PlaceInfo place) =>
            PickDestination(place.IsWorld ? WorldUrl(place.world_name) : null, place.base_position_processed, landOnParcel: false);

        private void OnLiveEventClicked(int index) =>
            OpenEventDetails(liveEvents[index]);

        private void OnUpcomingEventClicked(int index) =>
            OpenEventDetails(upcomingEvents[index]);

        private void OnUpcomingEventAddToCalendar(int index) =>
            eventCardActions.AddEventToCalendar(upcomingEvents[index]);

        // The menu is the uGUI one the Explore cards open: it stacks on the panel as a popup, anchored to the button in screen pixels
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

        // The details open in the same modal the Explore menu uses; jumping in from there comes back through OnEventJumpIn
        private void OpenEventDetails(EventDTO @event) =>
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
            // At startup there is no close button: leaving means the user is on their way in, which releases the flow
            leaving = true;
            inputData.JumpedIn?.Invoke();

            closeIntent?.TrySetResult();
            closeIntent = null;
        }

        // Dev only: leaves the screen for the other lobby implementation without releasing the startup flow, handing over the parameter it was shown with
        public LobbyParameter DevLeaveForSwitch()
        {
            leaving = true;
            closeIntent?.TrySetResult();
            closeIntent = null;
            return inputData;
        }

        private readonly struct PlaceholderFriend
        {
            public readonly string Name;
            public readonly string WalletTag;
            public readonly OnlineStatus Status;
            public readonly string Location;
            public readonly bool CanJoin;
            public readonly bool IsVerified;

            public PlaceholderFriend(string name, string walletTag, OnlineStatus status, string location, bool canJoin, bool isVerified)
            {
                Name = name;
                WalletTag = walletTag;
                Status = status;
                Location = location;
                CanJoin = canJoin;
                IsVerified = isVerified;
            }
        }
    }
}
