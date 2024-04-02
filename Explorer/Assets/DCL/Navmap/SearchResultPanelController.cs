using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Navmap
{
    public class SearchResultPanelController
    {
        public event Action<string> OnResultClicked;

        private readonly SearchResultPanelView view;
        private readonly IWebRequestController webRequestController;
        private ObjectPool<FullSearchResultsView> resultsPool;
        private readonly List<FullSearchResultsView> usedPoolElements;

        public SearchResultPanelController(SearchResultPanelView view, IWebRequestController webRequestController)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            usedPoolElements = new List<FullSearchResultsView>();
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            FullSearchResultsView asset = (await assetsProvisioner.ProvideInstanceAsync(view.ResultRef, ct: ct)).Value;

            resultsPool = new ObjectPool<FullSearchResultsView>(
                () => CreatePoolElements(asset),
                _ => { },
                defaultCapacity: 8
            );
        }

        private FullSearchResultsView CreatePoolElements(FullSearchResultsView asset)
        {
            FullSearchResultsView fullSearchResultsView = Object.Instantiate(asset, view.searchResultsContainer);
            fullSearchResultsView.ConfigurePlaceImageController(webRequestController);
            return fullSearchResultsView;
        }

        public void ShowLoading()
        {
            ReleasePool();
            view.LoadingContainer.SetActive(true);
            view.gameObject.SetActive(true);
        }

        public void Hide()
        {
            view.gameObject.SetActive(false);
        }

        public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ReleasePool();

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
                fullSearchResultsView.placeName.text = placeInfo.title;
                fullSearchResultsView.placeCreator.text = string.Format("created by <b>{0}</b>", placeInfo.contact_name);
                fullSearchResultsView.playersCount.text = placeInfo.user_count.ToString();
                fullSearchResultsView.SetPlaceImage(placeInfo.image);
                fullSearchResultsView.resultButton.onClick.AddListener(() => OnResultClicked?.Invoke(placeInfo.base_position));
            }

            view.LoadingContainer.SetActive(false);
        }

        private void ReleasePool()
        {
            foreach (FullSearchResultsView fullSearchResultsView in usedPoolElements)
            {
                fullSearchResultsView.resultButton.onClick.RemoveAllListeners();
                resultsPool.Release(fullSearchResultsView);
            }

            usedPoolElements.Clear();
        }
    }
}
