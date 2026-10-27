using DCL.PlacesAPIService;

namespace DCL.Places
{
    public class PlacesFilters
    {
        // Not a server category: it selects the curated set behind the destinations endpoint's only_highlighted flag.
        public const string FEATURED_CATEGORY_ID = "featured";

        public PlacesSection? Section;
        public string? CategoryId;
        public IPlacesAPIService.SortBy SortBy;
        public IPlacesAPIService.SDKVersion SDKVersion;
        public string SearchText;

        public bool IsBrowsingFeatured => CategoryId == FEATURED_CATEGORY_ID && string.IsNullOrEmpty(SearchText);
    }
}
