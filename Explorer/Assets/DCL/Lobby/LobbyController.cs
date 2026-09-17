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
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world starts loading and, later, on demand during gameplay.
    ///     It only reports the close intent (Jump in, Close or Logout); what happens next is up to the caller.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView, LobbyParameter>
    {
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
        private readonly IRealmNavigator realmNavigator;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly StartParcel startParcel;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly SidebarProfileButtonPresenter profileButtonPresenter;
        private readonly ProfileMenuController profileMenuController;

        private LobbyCharacterPreviewController? avatarPreview;
        private CancellationTokenSource? avatarCts;
        private CancellationTokenSource? profileMenuCts;
        private CancellationTokenSource? recentPlacesCts;
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
            IRealmNavigator realmNavigator,
            IDecentralandUrlsSource decentralandUrlsSource,
            StartParcel startParcel,
            ThumbnailLoader thumbnailLoader,
            SidebarProfileButtonPresenter profileButtonPresenter,
            ProfileMenuController profileMenuController) : base(viewFactory)
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
            this.realmNavigator = realmNavigator;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.startParcel = startParcel;
            this.thumbnailLoader = thumbnailLoader;
            this.profileButtonPresenter = profileButtonPresenter;
            this.profileMenuController = profileMenuController;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
            {
                viewInstance.JumpInButton.onClick.RemoveListener(RequestClose);
                viewInstance.CloseButton.onClick.RemoveListener(RequestClose);
                viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent -= OnAvatarClicked;
                viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.RemoveListener(ShowProfileMenu);

                foreach (LobbyPlaceCardView card in viewInstance.RecentPlaceCards)
                    card.Button.onClick.RemoveAllListeners();
            }

            avatarCts.SafeCancelAndDispose();
            recentPlacesCts.SafeCancelAndDispose();
            jumpInCts.SafeCancelAndDispose();
            profileMenuCts.SafeCancelAndDispose();
            avatarPreview?.Dispose();
            closeIntent?.TrySetCanceled();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.JumpInButton.onClick.AddListener(RequestClose);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent += OnAvatarClicked;
            viewInstance.ProfileWidgetView.OpenProfileButton.Button.onClick.AddListener(ShowProfileMenu);

            foreach (LobbyPlaceCardView card in viewInstance.RecentPlaceCards)
                card.Button.onClick.AddListener(() => OnRecentPlaceClicked(card));

            viewInstance.RecentPlacesSection.SetActive(false);

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

            recentPlacesCts = recentPlacesCts.SafeRestart();
            ShowRecentPlacesAsync(recentPlacesCts.Token).Forget();

            profileButtonPresenter.LoadProfile();
            profileMenuCts = profileMenuCts.SafeRestart();
            HideProfileMenuIfOpen();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            avatarCts.SafeCancelAndDispose();
            recentPlacesCts.SafeCancelAndDispose();
            avatarPreview!.OnHide();

            HideProfileMenuIfOpen();
            profileMenuCts.SafeCancelAndDispose();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            closeIntent = new UniTaskCompletionSource();

            await UniTask.WhenAny(closeIntent.Task.AttachExternalCancellation(ct),
                viewInstance!.ProfileMenuView.SystemMenuView.LogoutButton.OnClickAsync(ct));
        }

        private async UniTaskVoid ShowAvatarAsync(CancellationToken ct)
        {
            try
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (ct.IsCancellationRequested) return;

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

        private void OnProfileUpdated(Profile profile) =>
            avatarPreview!.Refresh(profile.Avatar);

        // Until the backpack gets its own modal the Explore panel takes over; being fullscreen it also closes this panel.
        private void OnAvatarClicked(PointerEventData _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)));

        private void OnRecentPlaceClicked(LobbyPlaceCardView card)
        {
            if (card.Place is not { } place) return;

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

        private void HideProfileMenuIfOpen()
        {
            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(CancellationToken.None).Forget();
        }

        private void ShowProfileMenu()
        {
            profileMenuCts = profileMenuCts.SafeRestart();

            if (profileMenuController.State != ControllerState.ViewHidden)
                return;

            ShowProfileMenuAsync(profileMenuCts.Token).Forget();
        }

        private async UniTaskVoid ShowProfileMenuAsync(CancellationToken ct)
        {
            try
            {
                viewInstance!.ProfileMenuCloserButton.gameObject.SetActive(true);
                viewInstance.ProfileMenuCloserButton.onClick.AddListener(OnProfileMenuCloserClicked);

                await profileMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Popup, 0), new ControllerNoData(), ct);
                await profileMenuController.HideViewAsync(CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (viewInstance != null)
                {
                    viewInstance.ProfileMenuCloserButton.onClick.RemoveListener(OnProfileMenuCloserClicked);
                    viewInstance.ProfileMenuCloserButton.gameObject.SetActive(false);
                }
            }
        }

        private void OnProfileMenuCloserClicked() =>
            profileMenuCts?.Cancel();

        private void RequestClose()
        {
            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }

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
