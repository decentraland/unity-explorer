using System;
using System.Globalization;

namespace DCL.EventsApi
{
    public static class EventDataParser
    {
        internal static void ParseDeserializedDates(ref EventDTO eventDTO)
        {
            if (DateTime.TryParse(eventDTO.NextStartAt, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt))
                eventDTO.NextStartAtProcessed = nextStartAt;
            if (DateTime.TryParse(eventDTO.StartAt, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
                eventDTO.StartAtProcessed = startAt;

            if (eventDTO.RecurrentDates == null || eventDTO.RecurrentDates.Length == 0)
            {
                eventDTO.RecurrentDatesProcessed = Array.Empty<DateTime>();
                return;
            }

            eventDTO.RecurrentDatesProcessed = new DateTime[eventDTO.RecurrentDates.Length];

            for (var i = 0; i < eventDTO.RecurrentDates.Length; i++)
                if (DateTime.TryParse(eventDTO.RecurrentDates[i], null, DateTimeStyles.RoundtripKind, out DateTime date))
                    eventDTO.RecurrentDatesProcessed[i] = date;
        }

        internal static void ParseDeserializedDates(EventWithPlaceIdDTO eventDTO)
        {
            if (DateTime.TryParse(eventDTO.NextStartAt, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt))
                eventDTO.NextStartAtProcessed = nextStartAt;
            if (DateTime.TryParse(eventDTO.StartAt, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
                eventDTO.StartAtProcessed = startAt;

            if (eventDTO.RecurrentDates == null || eventDTO.RecurrentDates.Length == 0)
            {
                eventDTO.RecurrentDatesProcessed = Array.Empty<DateTime>();
                return;
            }

            eventDTO.RecurrentDatesProcessed = new DateTime[eventDTO.RecurrentDates.Length];

            for (var i = 0; i < eventDTO.RecurrentDates.Length; i++)
                if (DateTime.TryParse(eventDTO.RecurrentDates[i], null, DateTimeStyles.RoundtripKind, out DateTime date))
                    eventDTO.RecurrentDatesProcessed[i] = date;
        }
    }
}
