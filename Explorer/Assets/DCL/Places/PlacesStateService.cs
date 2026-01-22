using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlacesStateService : IDisposable
    {
        public Dictionary<string, PlacesData.PlaceInfo> CurrentPlaces { get; } = new();

        public PlacesData.PlaceInfo GetPlaceInfoById(string placeId) =>
            CurrentPlaces.GetValueOrDefault(placeId);

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            foreach (PlacesData.PlaceInfo place in places)
                CurrentPlaces[place.id] = place;
        }

        public void ClearPlaces() =>
            CurrentPlaces.Clear();

        public void Dispose() =>
            ClearPlaces();
    }
}
