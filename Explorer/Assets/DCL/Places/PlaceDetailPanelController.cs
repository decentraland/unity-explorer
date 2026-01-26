using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.MapRenderer;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class PlaceDetailPanelController : ControllerBase<PlaceDetailPanelView, PlaceDetailPanelParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ThumbnailLoader thumbnailLoader;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly PlacesCardSocialActionsController placesCardSocialActionsController;
        private readonly INavmapBus navmapBus;
        private readonly IMapPathEventBus mapPathEventBus;

        private string currentNavigationPlaceId = string.Empty;

        private CancellationTokenSource? panelCts;

        public PlaceDetailPanelController(
            ViewFactoryMethod viewFactory,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            PlacesCardSocialActionsController placesCardSocialActionsController,
            INavmapBus navmapBus,
            IMapPathEventBus mapPathEventBus) : base(viewFactory)
        {
            this.thumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.placesCardSocialActionsController = placesCardSocialActionsController;
            this.navmapBus = navmapBus;
            this.mapPathEventBus = mapPathEventBus;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.LikeToggleChanged += OnLikeToggleChanged;
            viewInstance.DislikeToggleChanged += DislikeToggleChanged;
            viewInstance.FavoriteToggleChanged += OnFavoriteToggleChanged;
            viewInstance.ShareButtonClicked += OnShareButtonClicked;
            viewInstance.CopyLinkButtonClicked += OnCopyLinkButtonClicked;
            viewInstance.JumpInButtonClicked += OnJumpInButtonClicked;
            viewInstance.StartNavigationButtonClicked += OnStartNavigationButtonClicked;
            viewInstance.ExitNavigationButtonClicked += OnExitNavigationButtonClicked;
            navmapBus.OnDestinationSelected += OnNavmapBusDestinationSelected;
            mapPathEventBus.OnRemovedDestination += OnMapPathEventBusRemovedDestination;
        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();

            viewInstance!.ConfigurePlaceData(
                placeInfo: inputData.PlaceData,
                isNavigating: currentNavigationPlaceId == inputData.PlaceData.id,
                thumbnailLoader: thumbnailLoader,
                cancellationToken: panelCts.Token,
                friends: inputData.ConnectedFriends,
                profileRepositoryWrapper: profileRepositoryWrapper);

            SetCreatorThumbnailAsync(panelCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();

            if (viewInstance == null)
                return;

            viewInstance.LikeToggleChanged -= OnLikeToggleChanged;
            viewInstance.DislikeToggleChanged -= DislikeToggleChanged;
            viewInstance.FavoriteToggleChanged -= OnFavoriteToggleChanged;
            viewInstance.ShareButtonClicked -= OnShareButtonClicked;
            viewInstance.CopyLinkButtonClicked -= OnCopyLinkButtonClicked;
            viewInstance.JumpInButtonClicked -= OnJumpInButtonClicked;
            viewInstance.StartNavigationButtonClicked -= OnStartNavigationButtonClicked;
            viewInstance.ExitNavigationButtonClicked -= OnExitNavigationButtonClicked;
            navmapBus.OnDestinationSelected -= OnNavmapBusDestinationSelected;
            mapPathEventBus.OnRemovedDestination -= OnMapPathEventBusRemovedDestination;
        }

        protected override void OnViewClose() =>
            panelCts.SafeCancelAndDispose();

        private async UniTaskVoid SetCreatorThumbnailAsync(CancellationToken ct)
        {
            Profile.CompactInfo? creatorProfile = null;

            if (!string.IsNullOrEmpty(inputData.PlaceData.owner))
                creatorProfile = await profileRepository.GetCompactAsync(inputData.PlaceData.owner, ct);

            viewInstance!.SetCreatorThumbnail(profileRepositoryWrapper, creatorProfile);
        }

        private void OnLikeToggleChanged(PlacesData.PlaceInfo placeInfo, bool likeValue)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.LikePlaceAsync(placeInfo, likeValue, inputData.SummonerPlaceCard, viewInstance, panelCts.Token).Forget();
        }

        private void DislikeToggleChanged(PlacesData.PlaceInfo placeInfo, bool dislikeValue)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.DislikePlaceAsync(placeInfo, dislikeValue, inputData.SummonerPlaceCard, viewInstance, panelCts.Token).Forget();
        }

        private void OnFavoriteToggleChanged(PlacesData.PlaceInfo placeInfo, bool favoriteValue)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.UpdateFavoritePlaceAsync(placeInfo, favoriteValue, inputData.SummonerPlaceCard, viewInstance, panelCts.Token).Forget();
        }

        private void OnShareButtonClicked(PlacesData.PlaceInfo placeInfo)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.SharePlace(placeInfo);
        }

        private void OnCopyLinkButtonClicked(PlacesData.PlaceInfo placeInfo)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.CopyPlaceLink(placeInfo);
        }

        private void OnJumpInButtonClicked(PlacesData.PlaceInfo placeInfo) =>
            placesCardSocialActionsController.JumpInPlace(placeInfo, CancellationToken.None);

        private void OnStartNavigationButtonClicked(PlacesData.PlaceInfo placeInfo)
        {
            panelCts = panelCts.SafeRestart();
            placesCardSocialActionsController.StartNavigationToPlace(placeInfo);
            viewInstance!.SetNavigation(true);
        }

        private void OnExitNavigationButtonClicked()
        {
            placesCardSocialActionsController.ExitNavigationToPlace();
            viewInstance!.SetNavigation(false);
        }

        private void OnNavmapBusDestinationSelected(PlacesData.PlaceInfo placeInfo) =>
            currentNavigationPlaceId = placeInfo.id;

        private void OnMapPathEventBusRemovedDestination() =>
            currentNavigationPlaceId = string.Empty;
    }
}
