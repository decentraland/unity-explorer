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

        private CancellationTokenSource? searchPlacesCancellationToken;

        public PlacesAndEventsPanelController(
            PlacesAndEventsPanelView view,
            NavmapSearchBarController searchBarController,
            SearchResultPanelController searchResultController)
        {
            this.view = view;
            this.searchBarController = searchBarController;
            this.searchResultController = searchResultController;
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
            searchResultController.Show();

            searchPlacesCancellationToken = searchPlacesCancellationToken.SafeRestart();
            searchBarController.SearchAndShowAsync(searchPlacesCancellationToken.Token).Forget();
        }
    }
}
