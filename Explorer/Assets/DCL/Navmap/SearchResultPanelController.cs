using DCL.PlacesAPIService;
using DCL.UI;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Navmap
{
    public class SearchResultPanelController
    {
        private readonly SearchResultPanelView view;
        private readonly List<FullSearchResultsView> usedPoolElements;
        private readonly ObjectPool<FullSearchResultsView> resultsPool;
        private readonly INavmapBus navmapBus;

        public SearchResultPanelController(SearchResultPanelView view,
            ObjectPool<FullSearchResultsView> resultsPool,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.resultsPool = resultsPool;
            this.navmapBus = navmapBus;
            usedPoolElements = new List<FullSearchResultsView>();
        }

        public void Show()
        {
            if (view.panelAnimator.GetCurrentAnimatorStateInfo(0).IsName("In"))
                return;

            view.NoResultsContainer.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            view.CanvasGroup.interactable = true;
            view.CanvasGroup.blocksRaycasts = true;
            view.panelAnimator.SetTrigger(UIAnimationHashes.IN);
        }

        public void ClearResults()
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

        public void Hide()
        {
            ClearResults();
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
            view.panelAnimator.SetTrigger(UIAnimationHashes.OUT);
        }

        public void SetLoadingState()
        {
            for (var i = 0; i < 8; i++)
            {
                FullSearchResultsView fullSearchResultsView = resultsPool.Get();
                usedPoolElements.Add(fullSearchResultsView);
            }
        }

        public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ClearResults();

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
                fullSearchResultsView.resultButton.onClick.AddListener(() => navmapBus.SelectPlace(placeInfo));
            }
        }
    }
}
