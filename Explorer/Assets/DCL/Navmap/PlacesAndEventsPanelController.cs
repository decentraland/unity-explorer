using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class PlacesAndEventsPanelController
    {
        private const float ANIMATION_DURATION = 0.5f;

        private readonly PlacesAndEventsPanelView view;
        private readonly NavmapSearchBarController searchBarController;
        private readonly SearchResultPanelController searchResultController;
        private readonly PlaceInfoPanelController placeInfoPanelController;
        private readonly EventInfoPanelController eventInfoPanelController;
        private readonly NavmapZoomController navmapZoomController;

        private CancellationTokenSource? searchPlacesCancellationToken;
        private CancellationTokenSource? collapseExpandCancellationToken;

        private bool isExpanded = false;

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
            view.CollapseButton.onClick.AddListener(() => Collapse());
            view.ExpandButton.onClick.AddListener(Expand);

            view.PointerEnter += DisableMapZoom;
            view.PointerExit += EnableMapZoom;
            searchBarController.TogglePanel += OnTogglePanel;
            Collapse(true);
        }

        private void OnTogglePanel(bool active)
        {
            switch (active)
            {
                case true when !isExpanded:
                    Expand();
                    break;
                case false when isExpanded:
                    Collapse(true);
                    break;
                case false:
                    view.CollapseButton.gameObject.SetActive(false);
                    view.ExpandButton.gameObject.SetActive(false);
                    break;
            }
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
            searchResultController.Show();
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

        public void Expand()
        {
            isExpanded = true;
            view.CollapseButton.gameObject.SetActive(true);
            view.ExpandButton.gameObject.SetActive(false);
            view.CollapseSection.gameObject.SetActive(true);

            RectTransform transform = (RectTransform)view.CollapseSection;

            collapseExpandCancellationToken = collapseExpandCancellationToken.SafeRestart();

            transform.DOAnchorPosX(0f, ANIMATION_DURATION)
                     .ToUniTask(cancellationToken: collapseExpandCancellationToken.Token)
                     .Forget();
        }

        private void Collapse(bool disableOnEnd = false)
        {
            isExpanded = false;
            view.CollapseButton.gameObject.SetActive(false);
            view.ExpandButton.gameObject.SetActive(true);
            searchBarController.DisableBack();
            RectTransform transform = (RectTransform)view.CollapseSection;

            collapseExpandCancellationToken = collapseExpandCancellationToken.SafeRestart();

            transform.DOAnchorPosX(transform.rect.width, ANIMATION_DURATION)
                     .OnComplete(() =>
                      {
                          if (disableOnEnd)
                          {
                              view.CollapseButton.gameObject.SetActive(false);
                              view.ExpandButton.gameObject.SetActive(false);
                          }
                      })
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
