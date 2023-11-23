using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PlacesAPIService;
using System;
using System.Threading;

public class NavmapSearchBarController : IDisposable
{
    private readonly SearchBarView view;
    private readonly IPlacesAPIService placesAPIService;
    private readonly SearchResultPanelController searchResultPanelController;

    private CancellationTokenSource cts;

    public NavmapSearchBarController(SearchBarView view, SearchResultPanelView searchResultPanelView, IPlacesAPIService placesAPIService, IAssetsProvisioner assetsProvisioner)
    {
        this.view = view;
        this.placesAPIService = placesAPIService;

        searchResultPanelController = new SearchResultPanelController(searchResultPanelView, assetsProvisioner);

        view.inputField.onValueChanged.AddListener(OnValueChanged);
        view.clearSearchButton.onClick.AddListener(ClearSearch);
    }

    private void OnValueChanged(string searchText)
    {
        if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
            searchResultPanelController.Hide();

        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        SearchAndShowAsync(searchText).Forget();
    }

    private async UniTaskVoid SearchAndShowAsync(string searchText)
    {
        await UniTask.Delay(1000, cancellationToken: cts.Token);
        searchResultPanelController.ShowLoading();
        using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlaces(searchText, 0, 8, cts.Token);
        searchResultPanelController.SetResults(response.Data);
    }

    private void ClearSearch()
    {
        view.inputField.SetTextWithoutNotify("");
        searchResultPanelController.Hide();
    }

    public void Dispose()
    {
        view.inputField.onSelect.RemoveAllListeners();
        view.inputField.onValueChanged.RemoveAllListeners();
        view.inputField.onSubmit.RemoveAllListeners();
        view.clearSearchButton.onClick.RemoveAllListeners();
    }
}
