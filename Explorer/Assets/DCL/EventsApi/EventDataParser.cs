using System;
using System.Globalization;

namespace DCL.EventsApi
{
    public static class EventDataParser
    {
        internal static void ParseDeserializedDates(ref EventDTO eventDTO)
        {
            if (DateTime.TryParse(eventDTO.Next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt))
                eventDTO.NextStartAtProcessed = nextStartAt;
            if (DateTime.TryParse(eventDTO.Start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
                eventDTO.StartAtProcessed = startAt;

            if (eventDTO.Recurrent_dates == null || eventDTO.Recurrent_dates.Length == 0)
            {
                eventDTO.RecurrentDatesProcessed = Array.Empty<DateTime>();
                return;
            }

            eventDTO.RecurrentDatesProcessed = new DateTime[eventDTO.Recurrent_dates.Length];

            for (var i = 0; i < eventDTO.Recurrent_dates.Length; i++)
                if (DateTime.TryParse(eventDTO.Recurrent_dates[i], null, DateTimeStyles.RoundtripKind, out DateTime date))
                    eventDTO.RecurrentDatesProcessed[i] = date;
        }

        internal static void ParseDeserializedDates(EventWithPlaceIdDTO eventDTO)
        {
            if (DateTime.TryParse(eventDTO.Next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt))
                eventDTO.NextStartAtProcessed = nextStartAt;
            if (DateTime.TryParse(eventDTO.Start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt))
                eventDTO.StartAtProcessed = startAt;

            if (eventDTO.Recurrent_dates == null || eventDTO.Recurrent_dates.Length == 0)
            {
                eventDTO.RecurrentDatesProcessed = Array.Empty<DateTime>();
                return;
            }

            eventDTO.RecurrentDatesProcessed = new DateTime[eventDTO.Recurrent_dates.Length];

            for (var i = 0; i < eventDTO.Recurrent_dates.Length; i++)
                if (DateTime.TryParse(eventDTO.Recurrent_dates[i], null, DateTimeStyles.RoundtripKind, out DateTime date))
                    eventDTO.RecurrentDatesProcessed[i] = date;
        }
    }
}
