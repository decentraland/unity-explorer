using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlacesResultsView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action? PlacesGridScrollAtTheBottom;

        [Header("Places Counter")]
        [SerializeField] private TMP_Text placesResultsCounter = null!;

        [Header("Places Grid")]
        [SerializeField] private LoopGridView placesResultsLoopGrid = null!;
        [SerializeField] private ScrollRect placesResultsScrollRect = null!;
        [SerializeField] private GameObject placesResultsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView placesResultsLoadingSpinner = null!;
        [SerializeField] private GameObject placesResultsLoadingMoreSpinner = null!;

        private PlacesStateService placesStateService = null!;
        private readonly List<string> currentPlacesIds = new ();
        private bool isResultsScrollPositionAtBottom => placesResultsScrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        private void Awake() =>
            placesResultsScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);

        private void OnDestroy() =>
            placesResultsScrollRect.onValueChanged.RemoveAllListeners();

        public void SetDependencies(PlacesStateService stateService) =>
            this.placesStateService = stateService;

        public void SetPlacesCounter(int placesTotalAmount) =>
            placesResultsCounter.text = $"Results ({placesTotalAmount})";

        public void SetPlacesCounterActive(bool isActive) =>
            placesResultsCounter.gameObject.SetActive(isActive);

        public void InitializePlacesGrid()
        {
            placesResultsLoopGrid.InitGridView(0, SetupPlaceResultCardByIndex);
            placesResultsLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void AddPlacesResultsItems(IReadOnlyList<PlacesData.PlaceInfo> places, bool resetPos)
        {
            foreach (PlacesData.PlaceInfo placeInfo in places)
                currentPlacesIds.Add(placeInfo.id);

            placesResultsLoopGrid.SetListItemCount(currentPlacesIds.Count, resetPos);

            SetPlacesGridAsEmpty(currentPlacesIds.Count == 0);

            if (resetPos)
                placesResultsLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearPlacesResults()
        {
            currentPlacesIds.Clear();
            placesResultsLoopGrid.SetListItemCount(0, false);
            SetPlacesGridAsEmpty(true);
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

        private LoopGridViewItem SetupPlaceResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            PlacesData.PlaceInfo placeInfo = placesStateService.GetPlaceInfoById(currentPlacesIds[index]);
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            //CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();

            // Setup card data
            gridItem.GetComponentInChildren<TMP_Text>().text = placeInfo.title;

            // Setup card events
            // ...

            return gridItem;
        }

        private void SetPlacesGridAsEmpty(bool isEmpty)
        {
            placesResultsEmptyContainer.SetActive(isEmpty);
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
