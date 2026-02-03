using DCL.EventsApi;
using System;
using System.Collections.Generic;

namespace DCL.Events
{
    public class EventsStateService : IDisposable
    {
        private readonly Dictionary<string, EventDTO> currentEvents = new();

        public EventDTO? GetEventInfoById(string eventId)
        {
            if (currentEvents.TryGetValue(eventId, out EventDTO eventInfo))
                return eventInfo;

            return null;
        }

        public void SetEvents(IReadOnlyList<EventDTO> events)
        {
            ClearEvents();

            foreach (EventDTO eventInfo in events)
                currentEvents[eventInfo.id] = eventInfo;
        }

        public void ClearEvents() =>
            currentEvents.Clear();

        public void Dispose()
        {

        }
    }
}
