using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.Input.Component;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Threading;
using Utility;

namespace DCL.Navmap
{
    public class NavmapSearchBarController : IDisposable
    {
        public event Action<string>? OnResultClicked;
        public event Action? OnSearchTextChanged;

        private readonly SearchBarView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly HistoryRecordPanelView historyRecordPanelView;
        private readonly SearchResultPanelController searchResultPanelController;
        private readonly IInputBlock inputBlock;
        private readonly ISearchHistory searchHistory;

        private CancellationTokenSource? searchCancellationToken;
        private bool isAlreadySelected;

        public NavmapSearchBarController(
            SearchBarView view,
            SearchResultPanelView searchResultPanelView,
            HistoryRecordPanelView historyRecordPanelView,
            IPlacesAPIService placesAPIService,
            FloatingPanelView floatingPanelView,
            IWebRequestController webRequestController,
            IInputBlock inputBlock,
            ISearchHistory searchHistory)
        {
            this.view = view;
            this.historyRecordPanelView = historyRecordPanelView;
            this.placesAPIService = placesAPIService;
            this.inputBlock = inputBlock;
            this.searchHistory = searchHistory;

            searchResultPanelController = new SearchResultPanelController(searchResultPanelView, webRequestController);
            searchResultPanelController.OnResultClicked += ClickedResult;

            historyRecordPanelView.OnClickedHistoryRecord += ClickedHistoryResult;

            view.inputField.onSelect.AddListener((_) => OnSelectedSearchbarChange(true));
            view.inputField.onDeselect.AddListener((_) => OnSelectedSearchbarChange(false));
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            floatingPanelView.closeButton.onClick.AddListener(ClearSearch);
            floatingPanelView.backButton.onClick.AddListener(() => searchResultPanelController.AnimateLeftRight(false));
            view.clearSearchButton.gameObject.SetActive(false);
            GetAndShowPreviousSearches();
            historyRecordPanelView.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            searchCancellationToken.SafeCancelAndDispose();
            view.inputField.onSelect.RemoveAllListeners();
            view.inputField.onValueChanged.RemoveAllListeners();
            view.inputField.onSubmit.RemoveAllListeners();
            view.clearSearchButton.onClick.RemoveAllListeners();
            searchResultPanelController.OnResultClicked -= ClickedResult;
        }

        private void ClickedHistoryResult(string historyText)
        {
            view.inputField.SetTextWithoutNotify(historyText);
            OnValueChanged(historyText);
            historyRecordPanelView.gameObject.SetActive(false);
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await searchResultPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

        private void ClickedResult(string coordinates)
        {
            searchResultPanelController.AnimateLeftRight(true);
            OnResultClicked?.Invoke(coordinates);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            OnSearchTextChanged?.Invoke();
            searchCancellationToken = searchCancellationToken.SafeRestart();

            if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
            {
                searchResultPanelController.Hide();
                historyRecordPanelView.gameObject.SetActive(true);
                return;
            }
            historyRecordPanelView.gameObject.SetActive(false);

            // Suppress cancellation but let other exceptions be printed
            SearchAndShowAsync(searchText, searchCancellationToken.Token)
               .SuppressCancellationThrow()
               .Forget();
        }

        public void ResetSearch()
        {
            searchResultPanelController.Reset();
            ClearSearch();
        }

        private void OnSelectedSearchbarChange(bool isSelected)
        {
            if (isSelected == isAlreadySelected)
                return;

            isAlreadySelected = isSelected;

            if (isSelected)
            {
                GetAndShowPreviousSearches();
                inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS);
            }
            else
            {
                inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS);
            }
        }

        private async UniTask SearchAndShowAsync(string searchText, CancellationToken ct)
        {
            searchResultPanelController.SetLoadingState();
            await UniTask.Delay(1000, cancellationToken: ct);
            searchResultPanelController.Show();

            searchHistory.Add(searchText);
            using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, 0, 8, ct);
            searchResultPanelController.SetResults(response.Data);
        }

        private void ClearSearch()
        {
            view.inputField.SetTextWithoutNotify("");
            searchResultPanelController.Hide();
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void GetAndShowPreviousSearches()
        {
            searchCancellationToken = searchCancellationToken.SafeRestart();
            string[] previousSearches = searchHistory.Get();
            if (previousSearches.Length <= 0) return;

            historyRecordPanelView.gameObject.SetActive(true);
            historyRecordPanelView.SetHistoryRecords(previousSearches);
        }
    }
}
