using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
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
        public event Action<string> OnResultClicked;

        private readonly SearchBarView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly FloatingPanelView floatingPanelView;
        private readonly SearchResultPanelController searchResultPanelController;

        private CancellationTokenSource cts;

        public NavmapSearchBarController(
            SearchBarView view,
            SearchResultPanelView searchResultPanelView,
            IPlacesAPIService placesAPIService,
            FloatingPanelView floatingPanelView,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.floatingPanelView = floatingPanelView;

            searchResultPanelController = new SearchResultPanelController(searchResultPanelView, webRequestController);
            searchResultPanelController.OnResultClicked += ClickedResult;
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
            floatingPanelView.closeButton.onClick.AddListener(ClearSearch);
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await searchResultPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

        private void ClickedResult(string coordinates)
        {
            floatingPanelView.backButton.gameObject.SetActive(true);
            OnResultClicked?.Invoke(coordinates);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
                searchResultPanelController.Hide();

            floatingPanelView.gameObject.SetActive(false);
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            SearchAndShowAsync(searchText).Forget();
        }

        private async UniTaskVoid SearchAndShowAsync(string searchText)
        {
            await UniTask.Delay(1000, cancellationToken: cts.Token);
            ResetFloatingPanelStatus();
            searchResultPanelController.ShowLoading();
            using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, 0, 8, cts.Token);
            searchResultPanelController.SetResults(response.Data);
        }

        private void ClearSearch()
        {
            view.inputField.SetTextWithoutNotify("");
            searchResultPanelController.Hide();
            view.clearSearchButton.gameObject.SetActive(false);
            ResetFloatingPanelStatus();
        }

        private void ResetFloatingPanelStatus()
        {
            floatingPanelView.gameObject.SetActive(false);
            floatingPanelView.backButton.gameObject.SetActive(false);
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
