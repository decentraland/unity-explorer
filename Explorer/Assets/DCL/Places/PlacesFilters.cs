using DCL.PlacesAPIService;

namespace DCL.Places
{
    public class PlacesFilters
    {
        public PlacesSection? Section;
        public string? CategoryId;
        public IPlacesAPIService.SortBy SortBy;
        public IPlacesAPIService.SDKVersion SDKVersion;
        public string SearchText;
    }
}
