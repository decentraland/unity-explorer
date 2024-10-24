using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.Input.Component;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapSearchBarController : IDisposable
    {
        private const string PREVIOUS_SEARCHES_KEY = "previous_searches";
        private const int MAX_PREVIOUS_SEARCHES = 5;

        public event Action<string> OnResultClicked;
        public event Action OnSearchTextChanged;

        private readonly SearchBarView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly HistoryRecordPanelView historyRecordPanelView;
        private readonly SearchResultPanelController searchResultPanelController;
        private readonly IInputBlock inputBlock;

        private CancellationTokenSource cts;
        private bool isAlreadySelected;
        private string[] previousSearches;
        private string previousSearchesString;
        private string playerPrefsPreviousSearches;

        public NavmapSearchBarController(
            SearchBarView view,
            SearchResultPanelView searchResultPanelView,
            HistoryRecordPanelView historyRecordPanelView,
            IPlacesAPIService placesAPIService,
            FloatingPanelView floatingPanelView,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            IInputBlock inputBlock
        )
        {
            this.view = view;
            this.historyRecordPanelView = historyRecordPanelView;
            this.placesAPIService = placesAPIService;
            this.inputBlock = inputBlock;

            searchResultPanelController = new SearchResultPanelController(searchResultPanelView, webRequestController, getTextureArgsFactory);
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
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();

            if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
            {
                searchResultPanelController.Hide();
                historyRecordPanelView.gameObject.SetActive(true);
                return;
            }

            historyRecordPanelView.gameObject.SetActive(false);

            // Suppress cancellation but let other exceptions be printed
            SearchAndShowAsync(searchText).SuppressCancellationThrow().Forget();
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
            else { inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS); }
        }

        private async UniTask SearchAndShowAsync(string searchText)
        {
            searchResultPanelController.SetLoadingState();
            await UniTask.Delay(1000, cancellationToken: cts.Token);
            searchResultPanelController.Show();

            AddToPreviousSearch(searchText);
            using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, 0, 8, cts.Token);
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
            cts = cts.SafeRestart();
            previousSearches = GetPreviousSearches();
            if (previousSearches.Length <= 0) return;

            historyRecordPanelView.gameObject.SetActive(true);
            historyRecordPanelView.SetHistoryRecords(previousSearches);
        }

        private void AddToPreviousSearch(string searchToAdd)
        {
            playerPrefsPreviousSearches = PlayerPrefs.GetString(PREVIOUS_SEARCHES_KEY);
            previousSearches = string.IsNullOrEmpty(playerPrefsPreviousSearches) ? Array.Empty<string>() : playerPrefsPreviousSearches.Split('|');

            switch (previousSearches.Length)
            {
                case > 0 when previousSearches[0] == searchToAdd:
                    return;
                case < MAX_PREVIOUS_SEARCHES:
                    PlayerPrefs.SetString(PREVIOUS_SEARCHES_KEY, previousSearches.Length > 0 ? searchToAdd + "|" + string.Join("|", previousSearches) : searchToAdd);
                    break;
                default:
                    PlayerPrefs.SetString(PREVIOUS_SEARCHES_KEY, searchToAdd + "|" + string.Join("|", previousSearches.Take(4)));
                    break;
            }
        }

        private string[] GetPreviousSearches()
        {
            previousSearchesString = PlayerPrefs.GetString(PREVIOUS_SEARCHES_KEY, "");
            return string.IsNullOrEmpty(previousSearchesString) ? Array.Empty<string>() : previousSearchesString.Split('|');
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            view.inputField.onSelect.RemoveAllListeners();
            view.inputField.onValueChanged.RemoveAllListeners();
            view.inputField.onSubmit.RemoveAllListeners();
            view.clearSearchButton.onClick.RemoveAllListeners();
            searchResultPanelController.OnResultClicked -= ClickedResult;
        }
    }
}
