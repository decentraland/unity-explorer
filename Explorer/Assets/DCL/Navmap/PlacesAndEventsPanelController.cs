using Cysharp.Threading.Tasks;
using System.Threading;
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

        private CancellationTokenSource? searchPlacesCancellationToken;

        public PlacesAndEventsPanelController(
            PlacesAndEventsPanelView view,
            NavmapSearchBarController searchBarController,
            SearchResultPanelController searchResultController,
            PlaceInfoPanelController placeInfoPanelController,
            EventInfoPanelController eventInfoPanelController)
        {
            this.view = view;
            this.searchBarController = searchBarController;
            this.searchResultController = searchResultController;
            this.placeInfoPanelController = placeInfoPanelController;
            this.eventInfoPanelController = eventInfoPanelController;
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

        public enum Section
        {
            SEARCH,
            PLACE,
            EVENT,
        }
    }
}
