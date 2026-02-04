using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;

namespace DCL.Events
{
    public class EventsStateService : IDisposable
    {
        private readonly Dictionary<string, EventDTO> currentEvents = new();
        private readonly Dictionary<string, PlacesData.PlaceInfo> currentPlaces = new();

        public class EventWithPlaceData
        {
            public EventDTO EventInfo;
            public PlacesData.PlaceInfo? PlaceInfo;
        }

        public EventWithPlaceData? GetEventDataById(string eventId)
        {
            EventWithPlaceData result = new EventWithPlaceData();

            if (currentEvents.TryGetValue(eventId, out EventDTO eventInfo))
            {
                result.EventInfo = eventInfo;
                if (!string.IsNullOrEmpty(eventInfo.place_id))
                {
                    currentPlaces.TryGetValue(eventInfo.place_id, out PlacesData.PlaceInfo? placeInfo);
                    result.PlaceInfo = placeInfo;
                }

                return result;
            }

            return null;
        }

        public void AddEvents(IReadOnlyList<EventDTO> events, bool clearCurrentEvents = false)
        {
            if (clearCurrentEvents)
                ClearEvents();

            foreach (EventDTO eventInfo in events)
                currentEvents.TryAdd(eventInfo.id, eventInfo);
        }

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places, bool clearCurrentPlaces = false)
        {
            if (clearCurrentPlaces)
                ClearPlaces();

            foreach (PlacesData.PlaceInfo placeInfo in places)
                currentPlaces.TryAdd(placeInfo.id, placeInfo);
        }

        public void ClearEvents() =>
            currentEvents.Clear();

        public void ClearPlaces() =>
            currentPlaces.Clear();

        public void Dispose()
        {
            ClearEvents();
            ClearPlaces();
        }
    }
}
