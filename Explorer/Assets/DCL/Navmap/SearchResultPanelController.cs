using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using DCL.UI;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Navmap
{
    public class SearchResultPanelController
    {
        private readonly SearchResultPanelView view;
        private readonly Dictionary<string, PlaceElementView> usedPoolElements;
        private readonly ObjectPool<PlaceElementView> resultsPool;
        private readonly INavmapBus navmapBus;
        private CancellationTokenSource? showPlaceInfoCancellationToken;

        public SearchResultPanelController(SearchResultPanelView view,
            ObjectPool<PlaceElementView> resultsPool,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.resultsPool = resultsPool;
            this.navmapBus = navmapBus;
            usedPoolElements = new Dictionary<string, PlaceElementView>();
        }

        public void Show()
        {
            view.NoResultsContainer.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            view.CanvasGroup.interactable = true;
            view.CanvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            ClearResults();
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
            view.gameObject.SetActive(false);
        }

        public void ClearResults()
        {
            foreach ((string _, PlaceElementView fullSearchResultsView) in usedPoolElements)
            {
                fullSearchResultsView.resultButton.onClick.RemoveAllListeners();
                fullSearchResultsView.resultAnimator.Rebind();
                fullSearchResultsView.resultAnimator.Update(0f);
                resultsPool.Release(fullSearchResultsView);
            }

            usedPoolElements.Clear();
        }

        public void SetLoadingState()
        {
            for (var i = 0; i < 8; i++)
            {
                var key = i.ToString();
                if (usedPoolElements.ContainsKey(key)) continue;
                PlaceElementView placeElementView = resultsPool.Get();
                usedPoolElements.Add(key, placeElementView);
            }
        }

        public void SetResults(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            ClearResults();

            view.NoResultsContainer.gameObject.SetActive(places.Count == 0);

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                // Why two places with the same base position comes in the list?
                if (usedPoolElements.ContainsKey(placeInfo.base_position)) continue;

                PlaceElementView placeElementView = resultsPool.Get();
                usedPoolElements.Add(placeInfo.base_position, placeElementView);
                placeElementView.placeName.text = placeInfo.title;
                placeElementView.placeCreator.gameObject.SetActive(
                    !string.IsNullOrEmpty(placeInfo.contact_name) && placeInfo.contact_name != "Unknown");
                placeElementView.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
                placeElementView.playerCounterContainer.SetActive(placeInfo.user_count > 0);
                placeElementView.playersCount.text = placeInfo.user_count.ToString();
                placeElementView.resultAnimator.SetTrigger(UIAnimationHashes.LOADED);
                placeElementView.SetPlaceImage(placeInfo.image);
                placeElementView.resultButton.onClick.AddListener(() =>
                {
                    showPlaceInfoCancellationToken = showPlaceInfoCancellationToken.SafeRestart();
                    navmapBus.SelectPlaceAsync(placeInfo, showPlaceInfoCancellationToken.Token).Forget();
                });
                placeElementView.LiveContainer.SetActive(false);
            }
        }

        public void SetLiveEvents(HashSet<string> parcels)
        {
            foreach (string parcel in parcels)
            {
                if (!usedPoolElements.TryGetValue(parcel, out PlaceElementView element)) continue;
                element.LiveContainer.SetActive(true);
            }
        }
    }
}
