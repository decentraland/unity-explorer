using DCL.Prefs;
using System.Collections.Generic;

namespace DCL.PlacesAPIService
{
    public class RecentlyVisitedPlacesController
    {
        private const int MAX_RECENTLY_VISITED_PLACES = 20;

        private readonly List<string> recentlyVisitedPlaces = new (MAX_RECENTLY_VISITED_PLACES);

        public RecentlyVisitedPlacesController()
        {
            InitializeRecentlyVisitedPlaces();
        }

        public void AddRecentlyVisitedPlace(string placeId)
        {
            if (recentlyVisitedPlaces.Contains(placeId))
                recentlyVisitedPlaces.Remove(placeId);
            else
            {
                if (recentlyVisitedPlaces.Count == MAX_RECENTLY_VISITED_PLACES)
                    recentlyVisitedPlaces.RemoveAt(recentlyVisitedPlaces.Count - 1);
            }

            recentlyVisitedPlaces.Insert(0, placeId);

            DCLPlayerPrefs.SetString(DCLPrefKeys.RECENTLY_VISITED_PLACES, string.Join(",", recentlyVisitedPlaces));
            DCLPlayerPrefs.Save();
        }

        public List<string> GetRecentlyVisitedPlaces() =>
            recentlyVisitedPlaces;

        private void InitializeRecentlyVisitedPlaces()
        {
            string storedRecentlyVisitedPlacesSerialized = DCLPlayerPrefs.GetString(DCLPrefKeys.RECENTLY_VISITED_PLACES);
            string[] storedRecentlyVisitedPlacesArray = storedRecentlyVisitedPlacesSerialized.Split(',');

            recentlyVisitedPlaces.Clear();

            foreach (string placeId in storedRecentlyVisitedPlacesArray)
            {
                if (string.IsNullOrWhiteSpace(placeId))
                    continue;

                recentlyVisitedPlaces.Add(placeId);
            }
        }
    }
}
