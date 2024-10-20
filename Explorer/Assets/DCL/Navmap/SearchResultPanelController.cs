using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character.CharacterMotion.Components;
using DCL.PlacesAPIService;
using DCL.UI;
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
        private readonly SearchResultPanelView view;
        private readonly IWebRequestController webRequestController;
        private readonly List<FullSearchResultsView> usedPoolElements;
        private ObjectPool<FullSearchResultsView> resultsPool;
        public event Action<string> OnResultClicked;

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
            if (view.panelAnimator.GetCurrentAnimatorStateInfo(0).IsName("In"))
                return;

            view.NoResultsContainer.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            view.CanvasGroup.interactable = true;
            view.CanvasGroup.blocksRaycasts = true;
            ResetAnimator();
            view.panelAnimator.SetTrigger(UIAnimationHashes.IN);
        }

        public void Reset()
        {
            ResetAnimator();
            view.gameObject.SetActive(false);
        }

        public void ResetAnimator()
        {
            view.panelAnimator.Rebind();
            view.panelAnimator.Update(0f);
        }

        public void Hide()
        {
            ReleasePool();
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
            view.panelAnimator.SetTrigger(UIAnimationHashes.OUT);
        }

        public void AnimateLeftRight(bool left) =>
            view.panelAnimator.SetTrigger(left ? UIAnimationHashes.TO_LEFT : UIAnimationHashes.TO_RIGHT);

        public void SetLoadingState()
        {
            ReleasePool();

            for (var i = 0; i < 8; i++)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
            }
        }

        public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ReleasePool();
            view.NoResultsContainer.gameObject.SetActive(places.Count == 0);

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
                fullSearchResultsView.placeName.text = placeInfo.title;
                fullSearchResultsView.placeCreator.gameObject.SetActive(!string.IsNullOrEmpty(placeInfo.contact_name) && placeInfo.contact_name != "Unknown");
                fullSearchResultsView.placeCreator.text = string.Format("created by <b>{0}</b>", placeInfo.contact_name);
                fullSearchResultsView.playerCounterContainer.SetActive(placeInfo.user_count > 0);
                fullSearchResultsView.playersCount.text = placeInfo.user_count.ToString();
                fullSearchResultsView.resultAnimator.SetTrigger(UIAnimationHashes.LOADED);
                fullSearchResultsView.SetPlaceImage(placeInfo.image);
                fullSearchResultsView.resultButton.onClick.AddListener(() => OnResultClicked?.Invoke(placeInfo.base_position));
            }
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
