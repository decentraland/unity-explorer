using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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
        private static readonly int LOADED_TRIGGER = Animator.StringToHash("Loaded");

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
                actionOnGet: result => result.gameObject.SetActive(true),
                actionOnRelease: result => result.gameObject.SetActive(false),
                defaultCapacity: 8
            );
        }

        private FullSearchResultsView CreatePoolElements(FullSearchResultsView asset)
        {
            FullSearchResultsView fullSearchResultsView = Object.Instantiate(asset, view.searchResultsContainer);
            fullSearchResultsView.ConfigurePlaceImageController(webRequestController);
            return fullSearchResultsView;
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
        }

        public void Hide()
        {
            ReleasePool();
            view.gameObject.SetActive(false);
        }

        public void SetLoadingState()
        {
            view.gameObject.SetActive(true);
            ReleasePool();
            for(var i = 0; i < 8; i++)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
            }
        }

        public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ReleasePool();

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
                fullSearchResultsView.placeName.text = placeInfo.title;
                fullSearchResultsView.placeCreator.gameObject.SetActive(!string.IsNullOrEmpty(placeInfo.contact_name) && placeInfo.contact_name != "Unknown");
                fullSearchResultsView.placeCreator.text = string.Format("created by <b>{0}</b>", placeInfo.contact_name);
                fullSearchResultsView.playerCounterContainer.SetActive(placeInfo.user_count > 0);
                fullSearchResultsView.playersCount.text = placeInfo.user_count.ToString();
                fullSearchResultsView.resultAnimator.SetTrigger(LOADED_TRIGGER);
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
                fullSearchResultsView.resultAnimator.Rebind();
                fullSearchResultsView.resultAnimator.Update(0f);
                resultsPool.Release(fullSearchResultsView);
            }

            usedPoolElements.Clear();
        }
    }
}
