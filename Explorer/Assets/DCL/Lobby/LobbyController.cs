using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities;
using DCL.Diagnostics;
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
        private readonly IRealmNavigator realmNavigator;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly StartParcel startParcel;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;

        private LobbyCharacterPreviewController? avatarPreview;
        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? placesCts;
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

                viewInstance.RecommendedPlaces.PlaceClicked = null;
            }

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
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

            foreach (LobbyPlaceCardView card in viewInstance.RecentPlaceCards)
                card.Button.onClick.AddListener(() => { if (card.Place is { } place) OnPlaceClicked(place); });

            viewInstance.RecommendedPlaces.PlaceClicked = OnPlaceClicked;

            viewInstance.RecentPlacesSection.SetActive(false);
            viewInstance.RecommendedPlacesSection.SetActive(false);

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
            viewInstance!.HomeCard.ShowLoading();
            ShowHomePlaceAsync(placesCts.Token).Forget();
            ShowRecentPlacesAsync(placesCts.Token).Forget();
            ShowRecommendedPlacesAsync(placesCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            avatarCts.SafeCancelAndDispose();
            placesCts.SafeCancelAndDispose();
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

            viewInstance!.HomeCard.Show(place, thumbnailLoader, ct);
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
            var shown = 0;

            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetRecentlyVisitedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                IReadOnlyList<PlacesData.PlaceInfo> places = result.Value.Data;

                for (; shown < places.Count && shown < cards.Length; shown++)
                    cards[shown].Show(places[shown], thumbnailLoader, ct);
            }

            for (int i = shown; i < cards.Length; i++)
                cards[i].Hide();

            viewInstance.RecentPlacesSection.SetActive(shown > 0);
        }

        /// <summary>
        ///     Fills the "Recommended places" carousel with the featured (highlighted) destinations, the ones the Places menu tags as Featured.
        /// </summary>
        private async UniTaskVoid ShowRecommendedPlacesAsync(CancellationToken ct)
        {
            Result<PlacesData.IPlacesAPIResponse> result = await placesAPIService.GetHighlightedDestinationsAsync(ct)
                                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested) return;

            if (result.Success && result.Value.Data.Count > 0)
            {
                viewInstance!.RecommendedPlaces.Show(result.Value.Data, thumbnailLoader, ct);
                viewInstance.RecommendedPlacesSection.SetActive(true);
            }
            else
                viewInstance!.RecommendedPlacesSection.SetActive(false);
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

        private void OnHomeJumpInClicked()
        {
            if (viewInstance!.HomeCard.Place is { } place)
                OnPlaceClicked(place);
        }

        // Until the backpack gets its own modal the Explore panel takes over; being fullscreen it also closes this panel.
        private void OnAvatarClicked(PointerEventData _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)));

        private void OnPlaceClicked(PlacesData.PlaceInfo place)
        {
            if (startParcel.IsConsumed())
            {
                jumpInCts = jumpInCts.SafeRestart();
                JumpIn(place, jumpInCts.Token);
            }
            else
                AssignStartDestination(place);

            RequestClose();
        }

        private void JumpIn(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            if (place.IsWorld)
                realmNavigator.TryChangeRealmAsync(WorldUrl(place.world_name), ct, isWorld: true, allowsSpawnPointerOverride: true).Forget();
            else
                realmNavigator.TeleportToParcelAsync(place.base_position_processed, ct, false).Forget();
        }

        // Nothing is loaded yet: the startup teleport lands directly in the picked place
        private void AssignStartDestination(PlacesData.PlaceInfo place)
        {
            if (place.IsWorld)
                startParcel.AssignRealm(WorldUrl(place.world_name));
            else
            {
                startParcel.AssignRealm(URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)));
                startParcel.Assign(place.base_position_processed);
            }
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
