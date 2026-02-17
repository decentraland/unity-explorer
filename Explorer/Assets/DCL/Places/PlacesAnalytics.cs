using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.InputSystem;

namespace DCL.Places
{
    public class PlacesAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PlacesResultsController placesResultsController;
        private readonly PlacesCardSocialActionsController placeCardActionsController;

        public PlacesAnalytics(
            IAnalyticsController analytics,
            PlacesResultsController placesResultsController,
            PlacesCardSocialActionsController placeCardActionsController)
        {
            this.analytics = analytics;
            this.placesResultsController = placesResultsController;
            this.placeCardActionsController = placeCardActionsController;

            DCLInput.Instance.Shortcuts.Places.performed += OnPlacesShortcutPerformed;
            placesResultsController.PlacesSearched += OnPlacesSearched;
            placesResultsController.PlacesFiltered += OnPlacesFiltered;
            placesResultsController.PlaceClicked += OnPlaceClicked;
            placeCardActionsController.PlaceSetAsLiked += OnPlaceSetAsLiked;
            placeCardActionsController.PlaceSetAsDisliked += OnPlaceSetAsDisliked;
            placeCardActionsController.PlaceSetAsFavorite += OnPlaceSetAsFavorite;
            placeCardActionsController.PlaceSetAsHome += OnPlaceSetAsHome;
            placeCardActionsController.JumpedInPlace += OnJumpedInPlace;
            placeCardActionsController.PlaceShared += OnPlaceShared;
            placeCardActionsController.PlaceLinkCopied += OnPlaceLinkCopied;
            placeCardActionsController.NavigationToPlaceStarted += OnNavigationToPlaceStarted;
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.Places.performed -= OnPlacesShortcutPerformed;
            placesResultsController.PlacesSearched -= OnPlacesSearched;
            placesResultsController.PlacesFiltered -= OnPlacesFiltered;
            placesResultsController.PlaceClicked -= OnPlaceClicked;
            placeCardActionsController.PlaceSetAsLiked -= OnPlaceSetAsLiked;
            placeCardActionsController.PlaceSetAsDisliked -= OnPlaceSetAsDisliked;
            placeCardActionsController.PlaceSetAsFavorite -= OnPlaceSetAsFavorite;
            placeCardActionsController.PlaceSetAsHome -= OnPlaceSetAsHome;
            placeCardActionsController.JumpedInPlace -= OnJumpedInPlace;
            placeCardActionsController.PlaceShared -= OnPlaceShared;
            placeCardActionsController.PlaceLinkCopied -= OnPlaceLinkCopied;
            placeCardActionsController.NavigationToPlaceStarted -= OnNavigationToPlaceStarted;
        }

        private void OnPlacesShortcutPerformed(InputAction.CallbackContext _) =>
            analytics.Track(AnalyticsEvents.Places.PLACES_SECTION_OPENED, new JObject { { "source", "shortcut" } });

        private void OnPlacesSearched(string searchQuery, int resultsCount)
        {
            analytics.Track(AnalyticsEvents.Places.PLACES_SEARCHED, new JObject
            {
                { "search_query", searchQuery },
                { "results_count", resultsCount },
            });
        }

        private void OnPlacesFiltered(PlacesFilters filtersApplied)
        {
            analytics.Track(AnalyticsEvents.Places.PLACES_FILTERED, new JObject
            {
                { "section", filtersApplied.Section.ToString().ToLower() },
                { "category", string.IsNullOrEmpty(filtersApplied.CategoryId) ? "all" : filtersApplied.CategoryId },
                { "sortBy", filtersApplied.SortBy.ToString().ToLower() },
                { "sdkVersion", filtersApplied.SDKVersion.ToString().ToLower() },
            });
        }

        private void OnPlaceClicked(PlacesData.PlaceInfo placeInfo, PlaceCardView cardView, int resultsCount, PlacesFilters filtersApplied)
        {
            analytics.Track(AnalyticsEvents.Places.PLACE_CARD_CLICKED, new JObject
            {
                GetEventJObject(placeInfo),
                { "from_section", filtersApplied.Section.ToString().ToLower() },
                { "search_query", filtersApplied.SearchText },
                { "results_count", resultsCount },
                { "result_position", cardView.transform.GetSiblingIndex() + 1 },
            });
        }

        private void OnPlaceSetAsLiked(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_SET_AS_LIKED, GetEventJObject(placeInfo));

        private void OnPlaceSetAsDisliked(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_SET_AS_DISLIKED, GetEventJObject(placeInfo));

        private void OnPlaceSetAsFavorite(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_SET_AS_FAVORITE, GetEventJObject(placeInfo));

        private void OnPlaceSetAsHome(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_SET_AS_HOME, GetEventJObject(placeInfo));

        private void OnJumpedInPlace(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_JUMPED_IN, GetEventJObject(placeInfo));

        private void OnPlaceShared(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_SHARED, GetEventJObject(placeInfo));

        private void OnPlaceLinkCopied(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_LINK_COPIED, GetEventJObject(placeInfo));

        private void OnNavigationToPlaceStarted(PlacesData.PlaceInfo placeInfo) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_NAVIGATION_STARTED, GetEventJObject(placeInfo));

        private static JObject GetEventJObject(PlacesData.PlaceInfo placeInfo) =>
            new()
            {
                { "place_id", placeInfo.id },
                { "place_name", placeInfo.title },
                { "place_coords", string.IsNullOrWhiteSpace(placeInfo.world_name) ? placeInfo.base_position : placeInfo.world_name },
                { "highlighted", placeInfo.highlighted },
            };
    }
}
