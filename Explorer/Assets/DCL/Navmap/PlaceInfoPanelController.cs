using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.EventsApi;
using DCL.MapRenderer;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class PlaceInfoPanelController
    {
        private readonly PlaceInfoPanelView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly INavmapBus navmapBus;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ImageController thumbnailImage;
        private readonly MultiStateButtonController dislikeButton;
        private readonly MultiStateButtonController likeButton;
        private readonly MultiStateButtonController favoriteButton;
        private PlacesData.PlaceInfo? place;
        private CancellationTokenSource? favoriteCancellationToken;
        private CancellationTokenSource? rateCancellationToken;
        private Vector2Int? currentBaseParcel;
        private Vector2Int? destination;

        public PlaceInfoPanelController(PlaceInfoPanelView view,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            IMapPathEventBus mapPathEventBus,
            INavmapBus navmapBus,
            IChatMessagesBus chatMessagesBus)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.mapPathEventBus = mapPathEventBus;
            this.navmapBus = navmapBus;
            this.chatMessagesBus = chatMessagesBus;
            thumbnailImage = new ImageController(view.Thumbnail, webRequestController);

            navmapBus.OnDestinationSelected += SetDestination;

            view.EventsTabButton.onClick.AddListener(() =>
            {
                Toggle(Section.EVENTS);
                FetchEvents();
            });

            view.PhotosTabButton.onClick.AddListener(() =>
            {
                Toggle(Section.PHOTOS);
                FetchPhotos();
            });

            view.OverviewTabButton.onClick.AddListener(() => Toggle(Section.OVERVIEW));

            dislikeButton = new MultiStateButtonController(view.DislikeButton, true);
            dislikeButton.OnButtonClicked += Rate;

            likeButton = new MultiStateButtonController(view.LikeButton, true);
            likeButton.OnButtonClicked += Rate;

            favoriteButton = new MultiStateButtonController(view.FavoriteButton, true);
            favoriteButton.OnButtonClicked += SetAsFavorite;

            view.ShareButton.onClick.AddListener(Share);
            view.SetAsHomeButton.onClick.AddListener(SetAsHome);
            view.JumpInButton.onClick.AddListener(JumpIn);
            view.StartNavigationButton.onClick.AddListener(StartNavigation);
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
        }

        public void Hide()
        {
            view.gameObject.SetActive(false);
        }

        public void Set(PlacesData.PlaceInfo place)
        {
            this.place = place;

            if (VectorUtilities.TryParseVector2Int(place.base_position, out Vector2Int result))
                currentBaseParcel = result;
            else
                currentBaseParcel = null;

            thumbnailImage.RequestImage(place.image);
            view.PlaceNameLabel.text = place.title;
            view.CreatorNameLabel.text = $"created by <b>{place.contact_name}</b>";
            view.LikeRateLabel.text = place.like_rate;
            view.PlayerCountLabel.text = place.user_count.ToString();
            view.DescriptionLabel.text = place.description;
            view.CoordinatesLabel.text = place.base_position;
            view.ParcelCountLabel.text = place.Positions.Length.ToString();
            view.AppearsOnContainer.SetActive(place.categories.Length > 0);

            SetCategories(place);

            favoriteCancellationToken = favoriteCancellationToken.SafeRestart();
            UpdateFavoriteStatusAsync(favoriteCancellationToken.Token).Forget();
        }

        public void SetLiveEvent(EventDTO @event)
        {
            view.LiveEventContainer.SetActive(true);
            view.LiveEventNameLabel.text = @event.name;
        }

        public void HideLiveEvent()
        {
            view.LiveEventContainer.SetActive(false);
        }

        public void Toggle(Section section)
        {
            view.EventsTabContainer.SetActive(section == Section.EVENTS);
            view.EventsTabSelected.SetActive(section == Section.EVENTS);
            view.OverviewTabContainer.SetActive(section == Section.OVERVIEW);
            view.OverviewTabSelected.SetActive(section == Section.OVERVIEW);
            view.PhotosTabContainer.SetActive(section == Section.PHOTOS);
            view.PhotosTabSelected.SetActive(section == Section.PHOTOS);
        }

        private void SetCategories(PlacesData.PlaceInfo place)
        {
            foreach (PlaceInfoPanelView.AppearsOnCategory appearsOnCategory in view.AppearsOnCategories)
                appearsOnCategory.container.SetActive(false);

            foreach (string category in place.categories)
            foreach (PlaceInfoPanelView.AppearsOnCategory appearsOnCategory in view.AppearsOnCategories)
                if (appearsOnCategory.category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    appearsOnCategory.container.SetActive(true);
        }

        private async UniTaskVoid UpdateFavoriteStatusAsync(CancellationToken ct)
        {
            bool isFavorite = await placesAPIService.IsFavoritePlaceAsync(place!, ct);
            favoriteButton.SetButtonState(isFavorite);
        }

        private void SetAsFavorite(bool isFavorite)
        {
            favoriteCancellationToken = favoriteCancellationToken.SafeRestart();
            SetAsFavoriteAsync(favoriteCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SetAsFavoriteAsync(CancellationToken ct)
            {
                await placesAPIService.SetPlaceFavoriteAsync(place!.id, isFavorite, ct);
                favoriteButton.SetButtonState(isFavorite);
            }
        }

        private void StartNavigation()
        {
            if (place == null) return;
            navmapBus.SelectDestination(place);
        }

        private void SetDestination(PlacesData.PlaceInfo place)
        {
            if (VectorUtilities.TryParseVector2Int(place.base_position, out Vector2Int result))
                destination = result;
            else
                destination = null;
        }

        private void SetAsHome()
        {
            // TODO
        }

        private void JumpIn()
        {
            if (destination == currentBaseParcel)
                mapPathEventBus.ArrivedToDestination();

            navmapBus.JumpIn(place!);
            chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} {currentBaseParcel?.x},{currentBaseParcel?.y}", "jump in");
        }

        private void Share() { }

        private void Rate(bool isLike)
        {
            rateCancellationToken = rateCancellationToken.SafeRestart();
            RateAsync(rateCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RateAsync(CancellationToken ct)
            {
                await placesAPIService.RatePlace(isLike, place!.id, ct);
                likeButton.SetButtonState(isLike);
                dislikeButton.SetButtonState(!isLike);
            }
        }

        private void FetchEvents()
        {
            // TODO
        }

        private void FetchPhotos()
        {
            // TODO
        }

        public enum Section
        {
            OVERVIEW,
            PHOTOS,
            EVENTS,
        }
    }
}
