using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
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

        private CancellationTokenSource? searchPlacesCancellationToken;

        public PlacesAndEventsPanelController(
            PlacesAndEventsPanelView view,
            NavmapSearchBarController searchBarController,
            SearchResultPanelController searchResultController,
            PlaceInfoPanelController placeInfoPanelController)
        {
            this.view = view;
            this.searchBarController = searchBarController;
            this.searchResultController = searchResultController;
            this.placeInfoPanelController = placeInfoPanelController;
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
                case Section.Search:
                    searchResultController.Show();
                    placeInfoPanelController.Hide();
                    break;
                case Section.Place:
                    searchResultController.Hide();
                    placeInfoPanelController.Show();
                    break;
            }
        }

        public enum Section
        {
            Search,
            Place,
            Event
        }
    }
}
