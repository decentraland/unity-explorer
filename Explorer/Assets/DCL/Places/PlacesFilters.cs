using DCL.PlacesAPIService;

namespace DCL.Places
{
    public class PlacesFilters
    {
        public PlacesSection? Section;
        public string? CategoryId;
        public IPlacesAPIService.SortBy SortBy;
    }
}
