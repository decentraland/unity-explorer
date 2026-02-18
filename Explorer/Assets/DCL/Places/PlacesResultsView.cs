using DCL.Audio;
using DCL.Communities;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Places
{
    public class PlacesResultsView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action? BackButtonClicked;
        public event Action? ExplorePlacesClicked;
        public event Action? GetANameClicked;
        public event Action? PlacesGridScrollAtTheBottom;
        public event Action<PlacesData.PlaceInfo, bool, PlaceCardView>? PlaceLikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool, PlaceCardView>? PlaceDislikeToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool, PlaceCardView>? PlaceFavoriteToggleChanged;
        public event Action<PlacesData.PlaceInfo, bool, PlaceCardView>? PlaceHomeToggleChanged;
        public event Action<PlacesData.PlaceInfo>? PlaceJumpInButtonClicked;
        public event Action<PlacesData.PlaceInfo>? PlaceShareButtonClicked;
        public event Action<PlacesData.PlaceInfo>? PlaceCopyLinkButtonClicked;
        public event Action<PlacesData.PlaceInfo, PlaceCardView>? MainButtonClicked;

        private ThumbnailLoader? placesCardsThumbnailLoader;
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private PlacesData.PlaceInfo? lastClickedPlaceCtx;
        private GenericContextMenu? contextMenu;
        private CancellationTokenSource? openContextMenuCts;
        private HomePlaceEventBus? homePlaceEventBus;

        [Header("Places Counter")]
        [SerializeField] private GameObject placesResultsCounterContainer = null!;
        [SerializeField] private TMP_Text placesResultsCounter = null!;
        [SerializeField] private Button placesResultsBackButton = null!;

        [Header("Places Grid")]
        [SerializeField] private LoopGridView placesResultsLoopGrid = null!;
        [SerializeField] private ScrollRect placesResultsScrollRect = null!;
        [SerializeField] private GameObject placesResultsEmptyContainer = null!;
        [SerializeField] private GameObject favoritesResultsEmptyContainer = null!;
        [SerializeField] private GameObject myPlacesResultsEmptyContainer = null!;
        [SerializeField] private AudioClipConfig clickOnLinksAudio = null!;
        [SerializeField] private SkeletonLoadingView placesResultsLoadingSpinner = null!;
        [SerializeField] private GameObject placesResultsLoadingMoreSpinner = null!;
        [SerializeField] private Button explorePlacesFromEmptySearchButton = null!;
        [SerializeField] private Button explorePlacesFromEmptyFavoritesButton = null!;
        [SerializeField] private Button getANameButton = null!;

        [Header("Configuration")]
        [SerializeField] private PlaceContextMenuConfiguration placeCardContextMenuConfiguration = null!;

        private PlacesStateService placesStateService = null!;
        private readonly List<string> currentPlacesIds = new ();
        private bool isResultsScrollPositionAtBottom => placesResultsScrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        private void Awake()
        {
            placesResultsBackButton.onClick.AddListener(() => BackButtonClicked?.Invoke());
            placesResultsScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            explorePlacesFromEmptySearchButton.onClick.AddListener(() => ExplorePlacesClicked?.Invoke());
            explorePlacesFromEmptyFavoritesButton.onClick.AddListener(() => ExplorePlacesClicked?.Invoke());
            getANameButton.onClick.AddListener(() => GetANameClicked?.Invoke());

            contextMenu = new GenericContextMenu(placeCardContextMenuConfiguration.ContextMenuWidth, verticalLayoutPadding: placeCardContextMenuConfiguration.VerticalPadding, elementsSpacing: placeCardContextMenuConfiguration.ElementsSpacing)
                         .AddControl(new ButtonContextMenuControlSettings(placeCardContextMenuConfiguration.ShareText, placeCardContextMenuConfiguration.ShareSprite, () => PlaceShareButtonClicked?.Invoke(lastClickedPlaceCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(placeCardContextMenuConfiguration.CopyLinkText, placeCardContextMenuConfiguration.CopyLinkSprite, () => PlaceCopyLinkButtonClicked?.Invoke(lastClickedPlaceCtx!)));
        }

        private void OnDestroy()
        {
            placesResultsBackButton.onClick.RemoveAllListeners();
            placesResultsScrollRect.onValueChanged.RemoveAllListeners();
        }

        public void SetDependencies(
            PlacesStateService stateService,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepoWrapper,
            HomePlaceEventBus homeEventBus)
        {
            this.placesStateService = stateService;
            this.placesCardsThumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepoWrapper;
            this.homePlaceEventBus = homeEventBus;
        }

        public void SetPlacesCounter(string text, bool showBackButton = false)
        {
            placesResultsCounter.text = text;
            placesResultsBackButton.gameObject.SetActive(showBackButton);
        }

        public void SetPlacesCounterActive(bool isActive) =>
            placesResultsCounterContainer.SetActive(isActive);

        public void InitializePlacesGrid()
        {
            placesResultsLoopGrid.InitGridView(0, SetupPlaceResultCardByIndex);
            placesResultsLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void AddPlacesResultsItems(IReadOnlyList<PlacesData.PlaceInfo> places, bool resetPos, PlacesSection? section)
        {
            foreach (PlacesData.PlaceInfo placeInfo in places)
                currentPlacesIds.Add(placeInfo.id);

            placesResultsLoopGrid.SetListItemCount(currentPlacesIds.Count, resetPos);

            SetPlacesGridAsEmpty(currentPlacesIds.Count == 0, section);

            if (resetPos)
                placesResultsLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearPlacesResults(PlacesSection? section)
        {
            currentPlacesIds.Clear();
            placesResultsLoopGrid.SetListItemCount(0, false);
            SetPlacesGridAsEmpty(true, section);
        }

        public void SetPlacesGridAsLoading(bool isLoading)
        {
            if (isLoading)
                placesResultsLoadingSpinner.ShowLoading();
            else
                placesResultsLoadingSpinner.HideLoading();
        }

        public void SetPlacesGridLoadingMoreActive(bool isActive) =>
            placesResultsLoadingMoreSpinner.SetActive(isActive);

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(clickOnLinksAudio);

        public void RefreshOldPlaceAsHome(string newPlaceAsHomeId)
        {
            for (var i = 0; i < currentPlacesIds.Count; i++)
            {
                if (currentPlacesIds[i] != newPlaceAsHomeId)
                {
                    var item = placesResultsLoopGrid.GetShownItemByItemIndex(i);
                    if (item != null)
                    {
                        var cardView = item.GetComponent<PlaceCardView>();
                        if (cardView.IsSetAsHome)
                        {
                            cardView.SilentlySetHomeToggle(false);
                            break;
                        }
                    }
                }
            }
        }

        private LoopGridViewItem SetupPlaceResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            var placeInfoWithConnectedFriends = placesStateService.GetPlaceInfoById(currentPlacesIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            PlaceCardView cardView = gridItem.GetComponent<PlaceCardView>();

            // Setup card data
            bool isHome = homePlaceEventBus?.IsHome(placeInfoWithConnectedFriends.PlaceInfo) ?? false;
            cardView.Configure(
                placeInfo: placeInfoWithConnectedFriends.PlaceInfo,
                ownerName: placeInfoWithConnectedFriends.PlaceInfo.contact_name,
                userOwnsPlace: false,
                thumbnailLoader: placesCardsThumbnailLoader!,
                friends: placeInfoWithConnectedFriends.ConnectedFriends,
                profileRepositoryWrapper,
                isHome);

            // Setup card events
            cardView.SubscribeToInteractions(
                likeToggleChanged: (place, value, card) => PlaceLikeToggleChanged?.Invoke(place, value, card),
                dislikeToggleChanged: (place, value, card) => PlaceDislikeToggleChanged?.Invoke(place, value, card),
                favoriteToggleChanged: (place, value, card) => PlaceFavoriteToggleChanged?.Invoke(place, value, card),
                homeToggleChanged: (place, value, card) => PlaceHomeToggleChanged?.Invoke(place, value, card),
                shareButtonClicked: OpenCardContextMenu,
                infoButtonClicked: _ => { },
                jumpInButtonClicked: place => PlaceJumpInButtonClicked?.Invoke(place),
                deleteButtonClicked: _ => { },
                mainButtonClicked: (place, card) => MainButtonClicked?.Invoke(place, card));

            return gridItem;
        }

        private void OpenCardContextMenu(PlacesData.PlaceInfo placeInfo, Vector2 position, PlaceCardView placeCardView)
        {
            lastClickedPlaceCtx = placeInfo;
            placeCardView.CanPlayUnHoverAnimation = false;

            openContextMenuCts = openContextMenuCts.SafeRestart();
            ViewDependencies.ContextMenuOpener.OpenContextMenu(
                new GenericContextMenuParameter(contextMenu!, position, actionOnHide: () => placeCardView.CanPlayUnHoverAnimation = true), openContextMenuCts.Token);
        }

        private void SetPlacesGridAsEmpty(bool isEmpty, PlacesSection? section)
        {
            placesResultsEmptyContainer.SetActive(false);
            favoritesResultsEmptyContainer.SetActive(false);
            myPlacesResultsEmptyContainer.SetActive(false);

            switch (section)
            {
                case PlacesSection.FAVORITES:
                    favoritesResultsEmptyContainer.SetActive(isEmpty);
                    break;
                case PlacesSection.MY_PLACES:
                    myPlacesResultsEmptyContainer.SetActive(isEmpty);
                    break;
                default:
                    placesResultsEmptyContainer.SetActive(isEmpty);
                    break;
            }

            placesResultsLoopGrid.gameObject.SetActive(!isEmpty);
        }

        private void OnScrollRectValueChanged(Vector2 _)
        {
            if (!isResultsScrollPositionAtBottom)
                return;

            PlacesGridScrollAtTheBottom?.Invoke();
        }
    }
}
