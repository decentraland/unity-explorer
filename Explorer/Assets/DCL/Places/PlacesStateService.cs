using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlacesStateService : IDisposable
    {
        private readonly Dictionary<string, PlacesData.PlaceInfo> allPlaces = new();

        public PlacesData.PlaceInfo GetPlaceInfoById(string placeId) =>
            allPlaces.GetValueOrDefault(placeId);

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            foreach (PlacesData.PlaceInfo place in places)
                allPlaces[place.id] = place;
        }

        public void Dispose()
        {
            allPlaces.Clear();
        }
    }
}
