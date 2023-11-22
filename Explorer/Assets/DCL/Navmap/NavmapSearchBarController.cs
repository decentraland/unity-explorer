using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCLServices.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

public class NavmapSearchBarController : IDisposable
{
    private SearchBarView view;
    private readonly SearchResultPanelView searchResultPanelView;
    private readonly IPlacesAPIService placesAPIService;
    private readonly IAssetsProvisioner assetsProvisioner;
    private SearchResultPanelController searchResultPanelController;
    private CancellationTokenSource cts;

    public NavmapSearchBarController(SearchBarView view, SearchResultPanelView searchResultPanelView, IPlacesAPIService placesAPIService, IAssetsProvisioner assetsProvisioner)
    {
        this.view = view;
        this.searchResultPanelView = searchResultPanelView;
        this.placesAPIService = placesAPIService;
        this.assetsProvisioner = assetsProvisioner;

        searchResultPanelController = new SearchResultPanelController(searchResultPanelView, this.assetsProvisioner);

        view.inputField.onValueChanged.AddListener(OnValueChanged);
        view.inputField.onSubmit.AddListener(s => SubmitSearch(s));
        view.clearSearchButton.onClick.AddListener(() => ClearSearch());
        //view.inputField.onSelect.AddListener((text)=>OnSelected?.Invoke(true));
    }

    private void OnValueChanged(string searchText)
    {
        if (string.IsNullOrEmpty(searchText) || searchText.Length < 3)
            searchResultPanelController.Hide();

        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        SearchAndShow(searchText).Forget();
    }

    private async UniTaskVoid SearchAndShow(string searchText)
    {
        await UniTask.Delay(1000, cancellationToken: cts.Token);
        searchResultPanelController.ShowLoading();
        (IReadOnlyList<PlacesData.PlaceInfo> places, int total) searchPlaces = await placesAPIService.SearchPlaces(searchText, 0, 8, cts.Token);

        searchResultPanelController.SetResults(searchPlaces.places);
    }

    private void ClearSearch()
    {
        view.inputField.SetTextWithoutNotify("");
        searchResultPanelController.Hide();
    }

    private void SubmitSearch(string searchString)
    {
    }

    public void Dispose()
    {
        view.inputField.onSelect.RemoveAllListeners();
        view.inputField.onValueChanged.RemoveAllListeners();
        view.inputField.onSubmit.RemoveAllListeners();
        view.clearSearchButton.onClick.RemoveAllListeners();
    }
}
