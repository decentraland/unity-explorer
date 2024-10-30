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
        private readonly SearchBarView view;
        private readonly HistoryRecordPanelView historyRecordPanelView;
        private readonly IInputBlock inputBlock;
        private readonly ISearchHistory searchHistory;
        private readonly INavmapBus navmapBus;

        private CancellationTokenSource? searchCancellationToken;
        private bool isAlreadySelected;

        public NavmapSearchBarController(
            SearchBarView view,
            HistoryRecordPanelView historyRecordPanelView,
            IInputBlock inputBlock,
            ISearchHistory searchHistory,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.historyRecordPanelView = historyRecordPanelView;
            this.inputBlock = inputBlock;
            this.searchHistory = searchHistory;
            this.navmapBus = navmapBus;

            historyRecordPanelView.OnClickedHistoryRecord += ClickedHistoryResult;

            navmapBus.OnJumpIn += _ => ClearSearch();
            view.inputField.onSelect.AddListener(_ => OnSelectedSearchbarChange(true));
            view.inputField.onDeselect.AddListener(_ => OnSelectedSearchbarChange(false));
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
            ShowPreviousSearches();
            historyRecordPanelView.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            searchCancellationToken.SafeCancelAndDispose();
            view.inputField.onSelect.RemoveAllListeners();
            view.inputField.onValueChanged.RemoveAllListeners();
            view.inputField.onSubmit.RemoveAllListeners();
            view.clearSearchButton.onClick.RemoveAllListeners();
        }

        private void ClearSearch()
        {
            view.inputField.SetTextWithoutNotify("");
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void ClickedHistoryResult(string historyText)
        {
            view.inputField.SetTextWithoutNotify(historyText);
            OnValueChanged(historyText);
            historyRecordPanelView.gameObject.SetActive(false);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            searchCancellationToken = searchCancellationToken.SafeRestart();

            if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
            {
                historyRecordPanelView.gameObject.SetActive(true);
                return;
            }

            historyRecordPanelView.gameObject.SetActive(false);

            // Suppress cancellation but let other exceptions be printed
            SearchAndShowAsync(searchText, searchCancellationToken.Token)
               .SuppressCancellationThrow()
               .Forget();
        }

        private void OnSelectedSearchbarChange(bool isSelected)
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

        private async UniTask SearchAndShowAsync(string searchText, CancellationToken ct)
        {
            searchHistory.Add(searchText);
            await navmapBus.SearchForPlaceAsync(searchText, ct);
        }

        private void ShowPreviousSearches()
        {
            searchCancellationToken = searchCancellationToken.SafeRestart();
            string[] previousSearches = searchHistory.Get();
            if (previousSearches.Length <= 0) return;

            historyRecordPanelView.gameObject.SetActive(true);
            historyRecordPanelView.SetHistoryRecords(previousSearches);
        }
    }
}
