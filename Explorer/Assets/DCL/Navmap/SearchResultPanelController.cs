using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

public class SearchResultPanelController
{
    private readonly SearchResultPanelView view;
    private readonly IAssetsProvisioner assetsProvisioner;
    private ObjectPool<FullSearchResultsView> resultsPool;
    private readonly List<FullSearchResultsView> usedPoolElements;

    public SearchResultPanelController(SearchResultPanelView view, IAssetsProvisioner assetsProvisioner)
    {
        this.view = view;
        this.assetsProvisioner = assetsProvisioner;
        usedPoolElements = new List<FullSearchResultsView>();
        InitPoolAsync().Forget();
    }

    private async UniTaskVoid InitPoolAsync()
    {
        FullSearchResultsView asset = (await assetsProvisioner.ProvideMainAssetAsync(view.ResultAssetReference, ct: CancellationToken.None)).Value.GetComponent<FullSearchResultsView>();
        resultsPool = new ObjectPool<FullSearchResultsView>(
            () => Object.Instantiate(asset, view.searchResultsContainer),
            _ => { },
            defaultCapacity: 8
        );
    }

    public void Hide()
    {
        view.gameObject.SetActive(false);
    }

    public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
    {
        view.gameObject.SetActive(true);
        ReleasePool();
        foreach (PlacesData.PlaceInfo placeInfo in places)
        {
            FullSearchResultsView fullSearchResultsView = resultsPool.Get();
            usedPoolElements.Add(fullSearchResultsView);
            fullSearchResultsView.placeName.text = placeInfo.title;
            fullSearchResultsView.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            fullSearchResultsView.playersCount.text = placeInfo.user_count.ToString();
        }
    }

    private void ReleasePool()
    {
        foreach (FullSearchResultsView fullSearchResultsView in usedPoolElements)
        {
            resultsPool.Release(fullSearchResultsView);
        }
        usedPoolElements.Clear();
    }

}
