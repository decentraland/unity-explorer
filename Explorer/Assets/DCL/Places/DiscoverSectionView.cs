using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Places
{
    public class DiscoverSectionView : MonoBehaviour
    {
        private const string ALL_CATEGORY_ID = "all";

        public event Action<string?>? CategorySelected;

        [Header("Categories")]
        [SerializeField] private ButtonWithSelectableStateView categoryButtonPrefab = null!;
        [SerializeField] private Transform categoriesContainer = null!;

        [Header("Places")]
        [SerializeField] private LoopGridView placesResultLoopGrid = null!;
        [SerializeField] private GameObject placesResultsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView placesResultsLoadingSpinner = null!;
        [SerializeField] private GameObject placesResultsLoadingMoreSpinner = null!;

        private const int CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY = 15;

        private PlacesStateService placesStateService;
        private IObjectPool<ButtonWithSelectableStateView> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, ButtonWithSelectableStateView>> currentCategories = new ();
        private readonly List<string> currentPlacesIds = new ();

        private void Awake()
        {
            categoryButtonsPool = new ObjectPool<ButtonWithSelectableStateView>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView =>
                {
                    categoryButtonView.gameObject.SetActive(true);
                    categoryButtonView.transform.SetAsLastSibling();
                },
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));
        }

        public void SetDependencies(PlacesStateService stateService) =>
            this.placesStateService = stateService;

        public void SetActive(bool active) =>
            gameObject.SetActive(active);

        public void SetCategories(PlaceCategoryData[] categories)
        {
            var allCategoryData = new PlaceCategoryData
            {
                name = ALL_CATEGORY_ID,
                i18n = new PlaceCategoryLocalizationData
                {
                    en = "All",
                },
            };

            CreateAndSetupCategoryButton(allCategoryData);

            foreach (PlaceCategoryData categoryData in categories)
                CreateAndSetupCategoryButton(categoryData);

            SelectCategory(allCategoryData.name, invokeEvent: false);
        }

        public void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton.Value);

            currentCategories.Clear();
        }

        public void InitializePlacesGrid()
        {
            placesResultLoopGrid.InitGridView(0, SetupPlaceResultCardByIndex);
            placesResultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void AddPlacesResultsItems(IReadOnlyList<PlacesData.PlaceInfo> places, bool resetPos)
        {
            foreach (PlacesData.PlaceInfo placeInfo in places)
                currentPlacesIds.Add(placeInfo.id);

            placesResultLoopGrid.SetListItemCount(currentPlacesIds.Count, resetPos);

            SetPlacesGridAsEmpty(currentPlacesIds.Count == 0);

            if (resetPos)
                placesResultLoopGrid.ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void ClearPlacesResults()
        {
            currentPlacesIds.Clear();
            placesResultLoopGrid.SetListItemCount(0, false);
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

        private ButtonWithSelectableStateView InstantiateCategoryButtonPrefab()
        {
            ButtonWithSelectableStateView invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupCategoryButton(PlaceCategoryData categoryData)
        {
            ButtonWithSelectableStateView categoryButtonView = categoryButtonsPool.Get();

            // Setup card data
            categoryButtonView.Text.text = categoryData.i18n.en;

            // Setup card events
            categoryButtonView.Button.onClick.RemoveAllListeners();
            categoryButtonView.Button.onClick.AddListener(() => SelectCategory(categoryData.name));

            currentCategories.Add(new KeyValuePair<string, ButtonWithSelectableStateView>(categoryData.name, categoryButtonView));
        }

        private void SelectCategory(string categoryId, bool invokeEvent = true)
        {
            foreach (var category in currentCategories)
                category.Value.SetSelected(category.Key == categoryId);

            if (invokeEvent)
                CategorySelected?.Invoke(categoryId != ALL_CATEGORY_ID ? categoryId : null);
        }

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
            placesResultLoopGrid.gameObject.SetActive(!isEmpty);
        }
    }
}
