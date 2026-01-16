using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.SelectorButton;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Places
{
    public class PlacesView : MonoBehaviour
    {
        private const string SORT_BY_FILTER_MOST_ACTIVE_OPTION = "Most Active";
        private const string SORT_BY_FILTER_BEST_RATED_OPTION = "Best Rated";
        private const int SORT_BY_FILTER_DEFAULT_OPTION_INDEX = 1;
        private const string SORT_BY_SDK_VERSION_SDK7_OPTION = "SDK7 Only";
        private const string SORT_BY_SDK_VERSION_ALL_OPTION = "All";
        private const int SORT_BY_SDK_VERSION_FILTER_DEFAULT_OPTION_INDEX = 0;
        private const int SEARCH_AWAIT_TIME = 1000;
        private const int CATEGORY_BUTTONS_POOL_DEFAULT_CAPACITY = 15;
        private const string ALL_CATEGORY_ID = "all";
        private const float PLACES_RESULTS_TOP_Y_OFFSET_MAX = -20f;
        private const float PLACES_RESULTS_BOTTOM_Y_OFFSET_MAX = -80f;

        public event Action<PlacesFilters>? AnyFilterChanged;
        public event Action? SearchBarSelected;
        public event Action? SearchBarDeselected;

        public PlacesResultsView PlacesResultsView => placesResultsView;

        private IObjectPool<PlaceCategoryButton> categoryButtonsPool = null!;
        private readonly List<KeyValuePair<string, PlaceCategoryButton>> currentCategories = new ();
        private readonly PlacesFilters currentFilters = new ();

        [Header("Sections Tabs")]
        [SerializeField] private ButtonWithSelectableStateView discoverSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView favoritesSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView recentlyVisitedSectionTab = null!;
        [SerializeField] private ButtonWithSelectableStateView myPlacesSectionTab = null!;

        [Header("Filters")]
        [SerializeField] private TMP_Text sortByTitleText = null!;
        [SerializeField] private SelectorButtonView sortByDropdown = null!;
        [SerializeField] private TMP_Text sdkVersionTitleText = null!;
        [SerializeField] private SelectorButtonView sdkVersionDropdown = null!;
        [SerializeField] private SearchBarView searchBar = null!;

        [Header("Categories")]
        [SerializeField] private PlaceCategoryButton categoryButtonPrefab = null!;
        [SerializeField] private Transform categoriesContainer = null!;

        [Header("Places Results")]
        [SerializeField] private PlacesResultsView placesResultsView = null!;
        [SerializeField] private RectTransform placesResultsTransform = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        private CancellationTokenSource? searchCancellationCts;

        private void Awake()
        {
            // Tabs subscriptions
            discoverSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.DISCOVER));
            favoritesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.FAVORITES));
            recentlyVisitedSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.RECENTLY_VISITED));
            myPlacesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSection.MY_PLACES));

            // Filters subscriptions
            sortByDropdown.OptionClicked += OnSortByChanged;
            sdkVersionDropdown.OptionClicked += OnSDKVersionChanged;
            searchBar.inputField.onValueChanged.AddListener(OnSearchValueChanged);
            searchBar.inputField.onSubmit.AddListener(OnSearchSubmitted);
            searchBar.clearSearchButton.onClick.AddListener(OnSearchClearButtonClicked);
            searchBar.inputField.onSelect.AddListener(text => SearchBarSelected?.Invoke());
            searchBar.inputField.onDeselect.AddListener(text => SearchBarDeselected?.Invoke());

            // Categories pool configuration
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
            sdkVersionDropdown.OptionClicked -= OnSDKVersionChanged;
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            searchCancellationCts?.SafeCancelAndDispose();
        }

        public void SetViewActive(bool isActive)
        {
            gameObject.SetActive(isActive);

            if (!isActive)
                searchCancellationCts?.SafeCancelAndDispose();
        }

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

        public void OpenSection(PlacesSection section, bool force = false, bool invokeEvent = true, bool cleanSearch = true)
        {
            if (currentFilters.Section == section && !force)
                return;

            discoverSectionTab.SetSelected(false);
            favoritesSectionTab.SetSelected(false);
            recentlyVisitedSectionTab.SetSelected(false);
            myPlacesSectionTab.SetSelected(false);

            if (cleanSearch)
            {
                CleanSearchBar(raiseOnChangeEvent: false);
                currentFilters.SearchText = string.Empty;
            }

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

            currentFilters.Section = section;

            if (invokeEvent)
                AnyFilterChanged?.Invoke(currentFilters);
        }

        public void SetupSortByFilter()
        {
            sortByDropdown.SetOptions(new List<string> { SORT_BY_FILTER_MOST_ACTIVE_OPTION, SORT_BY_FILTER_BEST_RATED_OPTION });
            sortByDropdown.SelectedIndex = SORT_BY_FILTER_DEFAULT_OPTION_INDEX;
        }

        public void ClearSortByFilter() =>
            sortByDropdown.ClearOptions();

        public void SetSortByFilterVisible(bool isVisible)
        {
            sortByTitleText.gameObject.SetActive(isVisible);
            sortByDropdown.gameObject.SetActive(isVisible);
        }

        public void SetupSDKVersionFilter()
        {
            sdkVersionDropdown.SetOptions(new List<string> { SORT_BY_SDK_VERSION_SDK7_OPTION, SORT_BY_SDK_VERSION_ALL_OPTION });
            sdkVersionDropdown.SelectedIndex = SORT_BY_SDK_VERSION_FILTER_DEFAULT_OPTION_INDEX;
        }

        public void ClearSDKVersionFilter() =>
            sdkVersionDropdown.ClearOptions();

        public void SetSDKVersionFilterVisible(bool isVisible)
        {
            sdkVersionTitleText.gameObject.SetActive(isVisible);
            sdkVersionDropdown.gameObject.SetActive(isVisible);
        }

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
            currentFilters.Section = null;
            currentFilters.CategoryId = null;
            currentFilters.SortBy = IPlacesAPIService.SortBy.LIKE_SCORE;
            currentFilters.SDKVersion = IPlacesAPIService.SDKVersion.SDK7_ONLY;
            currentFilters.SearchText = string.Empty;
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

            if (currentFilters.CategoryId == selectedCategory && invokeEvent)
                return;

            currentFilters.CategoryId = selectedCategory;

            if (invokeEvent)
                AnyFilterChanged?.Invoke(currentFilters);
        }

        private void OnSortByChanged(int index)
        {
            var selectedSortBy = index == 0 ? IPlacesAPIService.SortBy.MOST_ACTIVE : IPlacesAPIService.SortBy.LIKE_SCORE;

            currentFilters.SortBy = selectedSortBy;
            AnyFilterChanged?.Invoke(currentFilters);
        }

        private void OnSDKVersionChanged(int index)
        {
            var selectedSDKVersion = index == 0 ? IPlacesAPIService.SDKVersion.SDK7_ONLY : IPlacesAPIService.SDKVersion.ALL;

            currentFilters.SDKVersion = selectedSDKVersion;
            AnyFilterChanged?.Invoke(currentFilters);
        }

        private void OnSearchValueChanged(string text)
        {
            searchCancellationCts = searchCancellationCts.SafeRestart();
            AwaitAndSendSearchAsync(text, searchCancellationCts.Token).Forget();
            SetSearchBarClearButtonActive(!string.IsNullOrEmpty(text));
        }

        private void OnSearchSubmitted(string text)
        {
            searchCancellationCts = searchCancellationCts.SafeRestart();
            AwaitAndSendSearchAsync(text, searchCancellationCts.Token, skipAwait: true).Forget();
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText, CancellationToken ct, bool skipAwait = false)
        {
            if (!skipAwait)
                await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: ct);

            currentFilters.SearchText = searchText;
            AnyFilterChanged?.Invoke(currentFilters);
        }

        private void OnSearchClearButtonClicked()
        {
            CleanSearchBar(raiseOnChangeEvent: false);
            currentFilters.SearchText = string.Empty;
            AnyFilterChanged?.Invoke(currentFilters);
        }

        private void CleanSearchBar(bool raiseOnChangeEvent = true)
        {
            TMP_InputField.OnChangeEvent originalEvent = searchBar.inputField.onValueChanged;

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = new TMP_InputField.OnChangeEvent();

            searchBar.inputField.text = string.Empty;
            SetSearchBarClearButtonActive(false);

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = originalEvent;
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);
    }
}
