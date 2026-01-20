using DCL.Audio;
using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlacesResultsView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action? BackButtonClicked;
        public event Action? PlacesGridScrollAtTheBottom;
        public Action<string>? MyPlacesResultsEmptySubTextClicked;

        private ThumbnailLoader? placesCardsThumbnailLoader;
        private CancellationToken placesCardsConfigurationCt;

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
        [SerializeField] private TMP_Text myPlacesResultsEmptySubText = null!;
        [SerializeField] private AudioClipConfig clickOnLinksAudio = null!;
        [SerializeField] private SkeletonLoadingView placesResultsLoadingSpinner = null!;
        [SerializeField] private GameObject placesResultsLoadingMoreSpinner = null!;

        private PlacesStateService placesStateService = null!;
        private readonly List<string> currentPlacesIds = new ();
        private bool isResultsScrollPositionAtBottom => placesResultsScrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        private void Awake()
        {
            placesResultsBackButton.onClick.AddListener(() => BackButtonClicked?.Invoke());
            placesResultsScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            myPlacesResultsEmptySubText.ConvertUrlsToClickeableLinks(OnMyPlacesResultsEmptySubTextClicked);
        }

        private void OnDestroy()
        {
            placesResultsBackButton.onClick.RemoveAllListeners();
            placesResultsScrollRect.onValueChanged.RemoveAllListeners();
        }

        public void SetDependencies(
            PlacesStateService stateService,
            ThumbnailLoader thumbnailLoader)
        {
            this.placesStateService = stateService;
            this.placesCardsThumbnailLoader = thumbnailLoader;
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

        public void AddPlacesResultsItems(IReadOnlyList<PlacesData.PlaceInfo> places, bool resetPos, PlacesSection? section, CancellationToken ct)
        {
            placesCardsConfigurationCt = ct;

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

        private LoopGridViewItem SetupPlaceResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            PlacesData.PlaceInfo placeInfo = placesStateService.GetPlaceInfoById(currentPlacesIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            PlaceCardView cardView = gridItem.GetComponent<PlaceCardView>();

            // Setup card data
            cardView.Configure(
                placeInfo: placeInfo,
                ownerName: placeInfo.contact_name,
                userOwnsPlace: false,
                thumbnailLoader: placesCardsThumbnailLoader!,
                ct: placesCardsConfigurationCt);

            // Setup card events
            // ...

            return gridItem;
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

        private void OnMyPlacesResultsEmptySubTextClicked(string id) =>
            MyPlacesResultsEmptySubTextClicked?.Invoke(id);
    }
}
