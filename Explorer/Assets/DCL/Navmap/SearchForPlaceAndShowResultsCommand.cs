using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using System.Threading;

namespace DCL.Navmap
{
    public class SearchForPlaceAndShowResultsCommand : INavmapCommand
    {
        private readonly IPlacesAPIService placesAPIService;
        private readonly SearchResultPanelController searchResultPanelController;
        private readonly ISearchHistory searchHistory;
        private readonly string searchText;
        private readonly int pageNumber;
        private readonly int pageSize;

        public SearchForPlaceAndShowResultsCommand(IPlacesAPIService placesAPIService,
            SearchResultPanelController searchResultPanelController,
            ISearchHistory searchHistory,
            string searchText,
            int pageNumber = 0,
            int pageSize = 8)
        {
            this.placesAPIService = placesAPIService;
            this.searchResultPanelController = searchResultPanelController;
            this.searchHistory = searchHistory;
            this.searchText = searchText;
            this.pageNumber = pageNumber;
            this.pageSize = pageSize;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            searchHistory.Add(searchText);

            searchResultPanelController.Show();

            searchResultPanelController.SetLoadingState();
            await UniTask.Delay(1000, cancellationToken: ct);

            using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, pageNumber, pageSize, ct);
            searchResultPanelController.SetResults(response.Data);
        }

        public void Undo()
        {
            searchResultPanelController.Hide();
            searchResultPanelController.ClearResults();
        }
    }
}
