using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.Navmap.ScriptableObjects;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapSearchBarController : IDisposable
    {
        private const int INPUT_DEBOUNCE_DELAY_MS = 1000;

        private readonly SearchBarView view;
        private readonly HistoryRecordPanelView historyRecordPanelView;
        private readonly SearchFiltersView searchFiltersView;
        private readonly IInputBlock inputBlock;
        private readonly ISearchHistory searchHistory;
        private readonly INavmapBus navmapBus;
        private readonly CategoryMappingSO categoryMappingSO;

        private CancellationTokenSource? searchCancellationToken;
        private CancellationTokenSource? backCancellationToken;
        private bool isAlreadySelected;
        private NavmapSearchPlaceFilter currentPlaceFilter = NavmapSearchPlaceFilter.All;
        private NavmapSearchPlaceSorting currentPlaceSorting = NavmapSearchPlaceSorting.MostActive;
        private string? currentCategory;
        private string currentSearchText = "";

        public bool Interactable
        {
            get => view.inputField.interactable;
            set => view.inputField.interactable = value;
        }

        public NavmapSearchBarController(
            SearchBarView view,
            HistoryRecordPanelView historyRecordPanelView,
            SearchFiltersView searchFiltersView,
            IInputBlock inputBlock,
            ISearchHistory searchHistory,
            INavmapBus navmapBus,
            CategoryMappingSO categoryMappingSO)
        {
            this.view = view;
            this.historyRecordPanelView = historyRecordPanelView;
            this.searchFiltersView = searchFiltersView;
            this.inputBlock = inputBlock;
            this.searchHistory = searchHistory;
            this.navmapBus = navmapBus;
            this.categoryMappingSO = categoryMappingSO;

            navmapBus.OnJumpIn += _ => ClearInput();
            navmapBus.OnFilterByCategory += SearchByCategory;
            view.inputField.onSelect.AddListener(_ => OnSearchBarSelected(true));
            view.inputField.onDeselect.AddListener(_ => OnSearchBarSelected(false));
            view.inputField.onValueChanged.AddListener(OnInputValueChanged);
            view.inputField.onSubmit.AddListener(OnSubmitSearch);
            view.clearSearchButton.onClick.AddListener(ClearInput);
            view.BackButton.onClick.AddListener(OnBackClicked);
            view.clearSearchButton.gameObject.SetActive(false);
            historyRecordPanelView.gameObject.SetActive(false);
            searchFiltersView.NewestButton.onClick.AddListener(() => Search(NavmapSearchPlaceSorting.Newest));
            searchFiltersView.BestRatedButton.onClick.AddListener(() => Search(NavmapSearchPlaceSorting.BestRated));
            searchFiltersView.MostActiveButton.onClick.AddListener(() => Search(NavmapSearchPlaceSorting.MostActive));
            searchFiltersView.Toggle(currentPlaceSorting);
            searchFiltersView.Toggle(currentPlaceFilter);
        }

        public void Dispose()
        {
            searchCancellationToken.SafeCancelAndDispose();
            view.inputField.onSelect.RemoveAllListeners();
            view.inputField.onValueChanged.RemoveAllListeners();
            view.inputField.onSubmit.RemoveAllListeners();
            view.clearSearchButton.onClick.RemoveAllListeners();
        }

        public void SetInputFieldCategory(string? category)
        {
            view.inputField.readOnly = !string.IsNullOrEmpty(category);
            view.inputFieldCategoryImage.gameObject.SetActive(!string.IsNullOrEmpty(category));

            if (string.IsNullOrEmpty(category))
                return;

            if(Enum.TryParse(category, true, out CategoriesEnum categoryEnum))
                view.inputFieldCategoryImage.sprite = categoryMappingSO.GetCategoryImage(categoryEnum);
        }

        public async UniTask DoDefaultSearch(CancellationToken ct)
        {
            currentSearchText = string.Empty;
            UpdateFilterAndSorting(NavmapSearchPlaceFilter.All, currentPlaceSorting);
            view.inputField.SetTextWithoutNotify(currentSearchText);

            await navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                page: 0,
                filter: currentPlaceFilter,
                sorting: currentPlaceSorting), ct);
        }

        public void SetInputText(string text)
        {
            view.inputField.SetTextWithoutNotify(text);
        }

        public void ClearInput()
        {
            view.inputField.SetTextWithoutNotify(string.Empty);

            view.clearSearchButton.gameObject.SetActive(false);

            searchCancellationToken = searchCancellationToken.SafeRestart();
            DoDefaultSearch(searchCancellationToken.Token).Forget();
        }

        public void EnableBack()
        {
            view.BackButton.gameObject.SetActive(true);
            view.SearchIcon.SetActive(false);
        }

        public void DisableBack()
        {
            view.BackButton.gameObject.SetActive(false);
            view.SearchIcon.SetActive(true);
        }

        public void HideHistoryResults() =>
            historyRecordPanelView.gameObject.SetActive(false);

        public void UpdateFilterAndSorting(NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting)
        {
            currentPlaceFilter = filter;
            searchFiltersView.Toggle(currentPlaceFilter);

            currentPlaceSorting = sorting;
            searchFiltersView.Toggle(currentPlaceSorting);
        }

        private void OnBackClicked()
        {
            backCancellationToken = backCancellationToken.SafeRestart();
            navmapBus.GoBackAsync(backCancellationToken.Token).Forget();
        }

        private void OnInputValueChanged(string searchText)
        {
            currentCategory = string.Empty;
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            currentSearchText = searchText;
        }

        private void OnSubmitSearch(string searchText)
        {
            searchCancellationToken = searchCancellationToken.SafeRestart();
            SearchAsync(searchCancellationToken.Token)
               .Forget();

            async UniTaskVoid SearchAsync(CancellationToken ct)
            {
                await UniTask.Delay(INPUT_DEBOUNCE_DELAY_MS, cancellationToken: ct).SuppressCancellationThrow();

                UpdateFilterAndSorting(NavmapSearchPlaceFilter.All, currentPlaceSorting);

                searchHistory.Add(searchText);

                await navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                                    page: 0,
                                    filter: currentPlaceFilter,
                                    sorting: currentPlaceSorting,
                                    text: currentSearchText), ct)
                               .SuppressCancellationThrow();
            }
        }

        private void OnSearchBarSelected(bool isSelected)
        {
            if (isSelected == isAlreadySelected)
                return;

            isAlreadySelected = isSelected;

            if (isSelected)
            {
                inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS);
            }
            else
            {
                inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS);

                WaitForClickThenHideHistoryResultsAsync(default).Forget();
            }

            return;

            async UniTaskVoid WaitForClickThenHideHistoryResultsAsync(CancellationToken ct)
            {
                await UniTask.DelayFrame(5, cancellationToken: ct);
                HideHistoryResults();
            }
        }

        private void Search(NavmapSearchPlaceSorting sorting)
        {
            UpdateFilterAndSorting(currentPlaceFilter, sorting);

            searchCancellationToken = searchCancellationToken.SafeRestart();

            navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                          page: 0,
                          text: currentSearchText,
                          filter: currentPlaceFilter,
                          sorting: sorting,
                          category: currentCategory), searchCancellationToken.Token)
                     .Forget();
        }

        private void SearchByCategory(string? category)
        {
            UpdateFilterAndSorting(category is "Favorites" ? NavmapSearchPlaceFilter.Favorites : NavmapSearchPlaceFilter.All, currentPlaceSorting);
            currentSearchText = string.Empty;
            currentCategory = category;
            searchCancellationToken = searchCancellationToken.SafeRestart();
            navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                          page: 0,
                          filter: currentPlaceFilter,
                          sorting: currentPlaceSorting,
                          category: category), searchCancellationToken.Token)
                     .Forget();
        }
    }
}
