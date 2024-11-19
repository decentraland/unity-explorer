using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class PlacesAndEventsPanelController
    {
        private readonly PlacesAndEventsPanelView view;
        private readonly NavmapSearchBarController searchBarController;
        private readonly SearchResultPanelController searchResultController;
        private readonly PlaceInfoPanelController placeInfoPanelController;
        private readonly EventInfoPanelController eventInfoPanelController;
        private readonly NavmapZoomController navmapZoomController;

        private CancellationTokenSource? searchPlacesCancellationToken;
        private CancellationTokenSource? collapseExpandCancellationToken;

        public PlacesAndEventsPanelController(
            PlacesAndEventsPanelView view,
            NavmapSearchBarController searchBarController,
            SearchResultPanelController searchResultController,
            PlaceInfoPanelController placeInfoPanelController,
            EventInfoPanelController eventInfoPanelController,
            NavmapZoomController navmapZoomController)
        {
            this.view = view;
            this.searchBarController = searchBarController;
            this.searchResultController = searchResultController;
            this.placeInfoPanelController = placeInfoPanelController;
            this.eventInfoPanelController = eventInfoPanelController;
            this.navmapZoomController = navmapZoomController;

            view.CollapseButton.gameObject.SetActive(true);
            view.ExpandButton.gameObject.SetActive(false);
            view.CollapseButton.onClick.AddListener(Collapse);
            view.ExpandButton.onClick.AddListener(Expand);

            view.PointerEnter += DisableMapZoom;
            view.PointerExit += EnableMapZoom;
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
            searchResultController.Show();

            searchPlacesCancellationToken = searchPlacesCancellationToken.SafeRestart();
            searchBarController.SearchAndShowAsync(searchPlacesCancellationToken.Token).Forget();
        }

        public void Toggle(Section section)
        {
            switch (section)
            {
                case Section.SEARCH:
                    searchResultController.Show();
                    placeInfoPanelController.Hide();
                    eventInfoPanelController.Hide();
                    break;
                case Section.PLACE:
                    searchResultController.Hide();
                    placeInfoPanelController.Show();
                    eventInfoPanelController.Hide();
                    break;
                case Section.EVENT:
                    searchResultController.Hide();
                    placeInfoPanelController.Hide();
                    eventInfoPanelController.Show();
                    break;
            }
        }

        private void Expand()
        {
            view.CollapseButton.gameObject.SetActive(true);
            view.ExpandButton.gameObject.SetActive(false);

            RectTransform transform = (RectTransform) view.transform;

            collapseExpandCancellationToken = collapseExpandCancellationToken.SafeRestart();

            transform.DOAnchorPosX(0f, 1f)
                     .ToUniTask(cancellationToken: collapseExpandCancellationToken.Token)
                     .Forget();
        }

        private void Collapse()
        {
            view.CollapseButton.gameObject.SetActive(false);
            view.ExpandButton.gameObject.SetActive(true);

            RectTransform transform = (RectTransform) view.transform;

            collapseExpandCancellationToken = collapseExpandCancellationToken.SafeRestart();

            transform.DOAnchorPosX(transform.rect.width, 1f)
                     .ToUniTask(cancellationToken: collapseExpandCancellationToken.Token)
                     .Forget();
        }

        private void EnableMapZoom() =>
            navmapZoomController.SetBlockZoom(false);

        private void DisableMapZoom() =>
            navmapZoomController.SetBlockZoom(true);

        public enum Section
        {
            SEARCH,
            PLACE,
            EVENT,
        }
    }
}
