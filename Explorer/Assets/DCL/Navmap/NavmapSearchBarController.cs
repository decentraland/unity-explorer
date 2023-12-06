using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PlacesAPIService;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class NavmapSearchBarController : IDisposable
    {
        public event Action<string> OnResultClicked;

        private readonly SearchBarView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly SearchResultPanelController searchResultPanelController;

        private CancellationTokenSource cts;

        public NavmapSearchBarController(SearchBarView view, SearchResultPanelView searchResultPanelView, IPlacesAPIService placesAPIService)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;

            searchResultPanelController = new SearchResultPanelController(searchResultPanelView);
            searchResultPanelController.OnResultClicked += ClickedResult;
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await searchResultPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

        private void ClickedResult(string coordinates)
        {
            OnResultClicked?.Invoke(coordinates);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
                searchResultPanelController.Hide();

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            SearchAndShowAsync(searchText).Forget();
        }

        private async UniTaskVoid SearchAndShowAsync(string searchText)
        {
            await UniTask.Delay(1000, cancellationToken: cts.Token);
            searchResultPanelController.ShowLoading();
            using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, 0, 8, cts.Token);
            searchResultPanelController.SetResults(response.Data);
        }

        private void ClearSearch()
        {
            view.inputField.SetTextWithoutNotify("");
            searchResultPanelController.Hide();
            view.clearSearchButton.gameObject.SetActive(false);
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
