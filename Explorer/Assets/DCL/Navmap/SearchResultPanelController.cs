using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PlacesAPIService;
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
