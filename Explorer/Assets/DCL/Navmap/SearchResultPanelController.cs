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
        private CancellationTokenSource? fetchMorePlacesCancellationToken;
        private INavmapBus.SearchPlaceParams paginationSearchParams;

        public SearchResultPanelController(SearchResultPanelView view,
            ObjectPool<PlaceElementView> resultsPool,
            INavmapBus navmapBus)
        {
            this.view = view;
            this.resultsPool = resultsPool;
            this.navmapBus = navmapBus;
            usedPoolElements = new Dictionary<string, PlaceElementView>();

            view.NextPageButton.onClick.AddListener(CycleToNextPage);
            view.PreviousPageButton.onClick.AddListener(CycleToPrevPage);
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
            view.PaginationContainer.SetActive(places.Count > 0);

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                // Why two places with the same base position comes in the list?
                if (usedPoolElements.ContainsKey(placeInfo.base_position)) continue;

                PlaceElementView placeElementView = resultsPool.Get();
                usedPoolElements.Add(placeInfo.base_position, placeElementView);
                placeElementView.transform.SetAsLastSibling();
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
                    navmapBus.SelectPlaceAsync(placeInfo, showPlaceInfoCancellationToken.Token, true).Forget();
                });
                placeElementView.LiveContainer.SetActive(false);
            }

            view.PaginationContainer.transform.SetAsLastSibling();
        }

        public void SetLiveEvents(HashSet<string> parcels)
        {
            foreach (string parcel in parcels)
            {
                if (!usedPoolElements.TryGetValue(parcel, out PlaceElementView element)) continue;
                element.LiveContainer.SetActive(true);
            }
        }

        public void SetPagination(int pageNumber, int pageSize, int totalResultCount,
            INavmapBus.SearchPlaceParams searchParams)
        {
            view.NextPageButton.gameObject.SetActive(totalResultCount >= pageSize);
            view.PreviousPageButton.gameObject.SetActive(totalResultCount >= pageSize);
            view.NextPageButton.interactable = totalResultCount / pageSize > pageNumber;
            view.PreviousPageButton.interactable = pageNumber > 0;
            paginationSearchParams = searchParams;
        }

        private void CycleToPrevPage()
        {
            fetchMorePlacesCancellationToken = fetchMorePlacesCancellationToken.SafeRestart();

            paginationSearchParams.page--;
            navmapBus.SearchForPlaceAsync(paginationSearchParams, fetchMorePlacesCancellationToken.Token);
        }

        private void CycleToNextPage()
        {
            fetchMorePlacesCancellationToken = fetchMorePlacesCancellationToken.SafeRestart();

            paginationSearchParams.page++;
            navmapBus.SearchForPlaceAsync(paginationSearchParams, fetchMorePlacesCancellationToken.Token);
        }
    }
}
