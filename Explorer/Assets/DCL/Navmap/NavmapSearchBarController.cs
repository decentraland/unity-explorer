using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.UI;
using System;
using System.Threading;
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

        private CancellationTokenSource? searchCancellationToken;
        private CancellationTokenSource? backCancellationToken;
        private bool isAlreadySelected;
        private NavmapSearchPlaceFilter currentPlaceFilter = NavmapSearchPlaceFilter.All;
        private NavmapSearchPlaceSorting currentPlaceSorting = NavmapSearchPlaceSorting.MostActive;
        private string currentSearchText = "";

        public bool Interactable
        {
            get => view.inputField.interactable;

            set
            {
                view.inputField.interactable = value;
                view.clearSearchButton.gameObject.SetActive(value && !string.IsNullOrEmpty(view.inputField.text));
            }
        }

        public NavmapSearchBarController(
            SearchBarView view,
            HistoryRecordPanelView historyRecordPanelView,
            SearchFiltersView searchFiltersView,
            IInputBlock inputBlock,
            ISearchHistory searchHistory,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.historyRecordPanelView = historyRecordPanelView;
            this.searchFiltersView = searchFiltersView;
            this.inputBlock = inputBlock;
            this.searchHistory = searchHistory;
            this.navmapBus = navmapBus;

            historyRecordPanelView.OnClickedHistoryRecord += ClickedHistoryResult;

            navmapBus.OnJumpIn += _ => ClearInput();
            navmapBus.OnFilterByCategory += SearchByCategory;
            view.inputField.onSelect.AddListener(_ => OnSearchBarSelected(true));
            view.inputField.onDeselect.AddListener(_ => OnSearchBarSelected(false));
            view.inputField.onValueChanged.AddListener(OnInputValueChanged);
            view.inputField.onSubmit.AddListener(OnInputValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearInput);
            view.BackButton.onClick.AddListener(OnBackClicked);
            view.clearSearchButton.gameObject.SetActive(false);
            ShowPreviousSearches();
            historyRecordPanelView.gameObject.SetActive(false);
            searchFiltersView.AllButton.onClick.AddListener(() => Search(NavmapSearchPlaceFilter.All));
            searchFiltersView.FavoritesButton.onClick.AddListener(() => Search(NavmapSearchPlaceFilter.Favorites));
            searchFiltersView.VisitedButton.onClick.AddListener(() => Search(NavmapSearchPlaceFilter.Visited));
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

        public async UniTask DoDefaultSearch(CancellationToken ct)
        {
            currentSearchText = string.Empty;
            UpdateFilterAndSorting(NavmapSearchPlaceFilter.All, NavmapSearchPlaceSorting.None);
            view.inputField.SetTextWithoutNotify(currentSearchText);

            await navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                page: 0,
                filter: currentPlaceFilter,
                sorting: currentPlaceSorting), ct);
        }

        public void SetInputText(string text) =>
            view.inputField.SetTextWithoutNotify(text);

        public void ClearInput()
        {
            view.inputField.SetTextWithoutNotify(string.Empty);

            view.clearSearchButton.gameObject.SetActive(false);
        }

        public void EnableBack()
        {
            view.BackButton.gameObject.SetActive(true);
        }

        public void DisableBack()
        {
            view.BackButton.gameObject.SetActive(false);
        }

        public void HideHistoryResults() =>
            historyRecordPanelView.gameObject.SetActive(false);

        private void OnBackClicked()
        {
            backCancellationToken = backCancellationToken.SafeRestart();
            navmapBus.GoBackAsync(backCancellationToken.Token).Forget();
        }

        private void ClickedHistoryResult(string historyText)
        {
            view.inputField.SetTextWithoutNotify(historyText);
            OnInputValueChanged(historyText);
            HideHistoryResults();
        }

        private void OnInputValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            searchCancellationToken = searchCancellationToken.SafeRestart();

            if (string.IsNullOrEmpty(searchText))
            {
                historyRecordPanelView.gameObject.SetActive(true);
                return;
            }

            HideHistoryResults();

            currentSearchText = searchText;

            SearchAsync(searchCancellationToken.Token)
               .Forget();

            return;

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
                ShowPreviousSearches();
                inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS);
            }
            else
                inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS);
        }

        private void ShowPreviousSearches()
        {
            searchCancellationToken = searchCancellationToken.SafeRestart();
            string[] previousSearches = searchHistory.Get();
            if (previousSearches.Length <= 0) return;

            historyRecordPanelView.gameObject.SetActive(true);
            historyRecordPanelView.SetHistoryRecords(previousSearches);
        }

        private void Search(NavmapSearchPlaceFilter filter)
        {
            UpdateFilterAndSorting(filter, currentPlaceSorting);

            searchCancellationToken = searchCancellationToken.SafeRestart();

            navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                          page: 0,
                          text: currentSearchText,
                          filter: filter,
                          sorting: currentPlaceSorting), searchCancellationToken.Token)
                     .Forget();
        }

        private void Search(NavmapSearchPlaceSorting sorting)
        {
            UpdateFilterAndSorting(currentPlaceFilter, sorting);

            searchCancellationToken = searchCancellationToken.SafeRestart();

            navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                          page: 0,
                          text: currentSearchText,
                          filter: currentPlaceFilter,
                          sorting: sorting), searchCancellationToken.Token)
                     .Forget();
        }

        private void SearchByCategory(string? category)
        {
            UpdateFilterAndSorting(NavmapSearchPlaceFilter.All, NavmapSearchPlaceSorting.None);
            currentSearchText = string.Empty;
            searchCancellationToken = searchCancellationToken.SafeRestart();

            navmapBus.SearchForPlaceAsync(INavmapBus.SearchPlaceParams.CreateWithDefaultParams(
                          page: 0,
                          filter: currentPlaceFilter,
                          sorting: currentPlaceSorting,
                          category: category), searchCancellationToken.Token)
                     .Forget();
        }

        private void UpdateFilterAndSorting(NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting)
        {
            currentPlaceFilter = filter;
            searchFiltersView.Toggle(currentPlaceFilter);

            bool isGlobalSearch = filter == NavmapSearchPlaceFilter.All;

            // Enable sorting only for filter: All
            if (!isGlobalSearch)
                sorting = NavmapSearchPlaceSorting.None;

            currentPlaceSorting = sorting;
            searchFiltersView.Toggle(currentPlaceSorting);

            searchFiltersView.NewestButton.interactable = isGlobalSearch;
            searchFiltersView.MostActiveButton.interactable = isGlobalSearch;
            searchFiltersView.BestRatedButton.interactable = isGlobalSearch;
        }
    }
}
