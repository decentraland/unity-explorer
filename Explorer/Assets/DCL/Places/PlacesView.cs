using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.SelectorButton;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Places
{
    public class PlacesView : MonoBehaviour
    {
        private const string SORT_BY_FILTER_MOST_ACTIVE_OPTION = "Most Active";
        private const string SORT_BY_FILTER_BEST_RATED_OPTION = "Best";
        private const int SORT_BY_FILTER_DEFAULT_OPTION_INDEX = 1;
        private const int CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY = 15;
        private const string ALL_CATEGORY_ID = "all";
        private const float PLACES_RESULTS_TOP_Y_OFFSET_MAX = -20f;
        private const float PLACES_RESULTS_BOTTOM_Y_OFFSET_MAX = -80f;

        public Action<PlacesSection>? SectionChanged;
        public event Action<string?>? CategorySelected;
        public event Action<IPlacesAPIService.SortBy>? SortByChanged;

        public PlacesResultsView PlacesResultsView => placesResultsView;
        public PlacesFilters CurrentFilters { get; } = new ();

        private IObjectPool<PlaceCategoryButton> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, PlaceCategoryButton>> currentCategories = new ();

        [Header("Sections Tabs")]
        [SerializeField] private ButtonWithSelectableStateView discoverSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView favoritesSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView recentlyVisitedSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView myPlacesSectionTab = null!;

        [Header("Filters")]
        [SerializeField] private SelectorButtonView sortByDropdown = null!;

        [Header("Categories")]
        [SerializeField] private PlaceCategoryButton categoryButtonPrefab = null!;
        [SerializeField] private Transform categoriesContainer = null!;

        [Header("Places Results")]
        [SerializeField] private PlacesResultsView placesResultsView = null!;
        [SerializeField] private RectTransform placesResultsTransform = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        private void Awake()
        {
            discoverSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.DISCOVER));
            favoritesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.FAVORITES));
            recentlyVisitedSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.RECENTLY_VISITED));
            myPlacesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.MY_PLACES));

            sortByDropdown.OptionClicked += OnSortByChanged;

            categoryButtonsPool = new ObjectPool<PlaceCategoryButton>(
                InstantiateCategoryButtonPrefab,
                defaultCapacity: CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY,
                actionOnGet: categoryButtonView =>
                {
                    categoryButtonView.gameObject.SetActive(true);
                    categoryButtonView.transform.SetAsLastSibling();
                },
                actionOnRelease: categoryButtonView => categoryButtonView.gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            discoverSectionTab.Button.onClick.RemoveAllListeners();
            favoritesSectionTab.Button.onClick.RemoveAllListeners();
            recentlyVisitedSectionTab.Button.onClick.RemoveAllListeners();
            myPlacesSectionTab.Button.onClick.RemoveAllListeners();
            sortByDropdown.OptionClicked -= OnSortByChanged;
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void OpenSection(PlacesSection section, bool force = false)
        {
            if (CurrentFilters.Section == section && !force)
                return;

            discoverSectionTab.SetSelected(false);
            favoritesSectionTab.SetSelected(false);
            recentlyVisitedSectionTab.SetSelected(false);
            myPlacesSectionTab.SetSelected(false);

            switch (section)
            {
                case PlacesSection.DISCOVER:
                    discoverSectionTab.SetSelected(true);
                    break;
                case PlacesSection.FAVORITES:
                    favoritesSectionTab.SetSelected(true);
                    break;
                case PlacesSection.RECENTLY_VISITED:
                    recentlyVisitedSectionTab.SetSelected(true);
                    break;
                case PlacesSection.MY_PLACES:
                    myPlacesSectionTab.SetSelected(true);
                    break;
            }

            CurrentFilters.Section = section;
            SectionChanged?.Invoke(section);
        }

        public void SetupSortByFilter()
        {
            sortByDropdown.SetOptions(new List<string> { SORT_BY_FILTER_MOST_ACTIVE_OPTION, SORT_BY_FILTER_BEST_RATED_OPTION });
            sortByDropdown.SelectedIndex = SORT_BY_FILTER_DEFAULT_OPTION_INDEX;
        }

        public void ClearSortByFilter() =>
            sortByDropdown.ClearOptions();

        public void SetCategories(PlaceCategoriesSO.PlaceCategoryData[] categories)
        {
            foreach (PlaceCategoriesSO.PlaceCategoryData categoryData in categories)
                CreateAndSetupCategoryButton(categoryData);

            SelectCategory(ALL_CATEGORY_ID, invokeEvent: false);
        }

        public void ClearCategories()
        {
            foreach (var categoryButton in currentCategories)
                categoryButtonsPool.Release(categoryButton.Value);

            currentCategories.Clear();
        }

        public void SetCategoriesVisible(bool isVisible)
        {
            categoriesContainer.gameObject.SetActive(isVisible);
            placesResultsTransform.offsetMax = new Vector2(placesResultsTransform.offsetMax.x, isVisible ? PLACES_RESULTS_BOTTOM_Y_OFFSET_MAX : PLACES_RESULTS_TOP_Y_OFFSET_MAX);
        }

        public void ResetCurrentFilters()
        {
            CurrentFilters.Section = null;
            CurrentFilters.CategoryId = null;
            CurrentFilters.SortBy = IPlacesAPIService.SortBy.LIKE_SCORE;
        }

        private PlaceCategoryButton InstantiateCategoryButtonPrefab()
        {
            PlaceCategoryButton invitedCommunityCardView = Instantiate(categoryButtonPrefab, categoriesContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupCategoryButton(PlaceCategoriesSO.PlaceCategoryData categoryData)
        {
            PlaceCategoryButton categoryButtonView = categoryButtonsPool.Get();

            // Setup card data
            categoryButtonView.Configure(categoryData);

            // Setup card events
            categoryButtonView.buttonView.Button.onClick.RemoveAllListeners();
            categoryButtonView.buttonView.Button.onClick.AddListener(() => SelectCategory(categoryData.id));

            currentCategories.Add(new KeyValuePair<string, PlaceCategoryButton>(categoryData.id, categoryButtonView));
        }

        private void SelectCategory(string categoryId, bool invokeEvent = true)
        {
            foreach (var category in currentCategories)
                category.Value.buttonView.SetSelected(category.Key == categoryId);

            string? selectedCategory = categoryId != ALL_CATEGORY_ID ? categoryId : null;

            if (CurrentFilters.CategoryId == selectedCategory && invokeEvent)
                return;

            CurrentFilters.CategoryId = selectedCategory;

            if (invokeEvent)
                CategorySelected?.Invoke(selectedCategory);
        }

        private void OnSortByChanged(int index)
        {
            var selectedSortBy = index == 0 ? IPlacesAPIService.SortBy.MOST_ACTIVE : IPlacesAPIService.SortBy.LIKE_SCORE;

            CurrentFilters.SortBy = selectedSortBy;
            SortByChanged?.Invoke(selectedSortBy);
        }
    }
}
