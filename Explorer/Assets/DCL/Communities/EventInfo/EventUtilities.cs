using DCL.EventsApi;
using System;
using System.Globalization;

namespace DCL.Communities.EventInfo
{
    public static class EventUtilities
    {
        private const string STARTED_EVENT_TIME_FORMAT = "Started {0} {1} ago";
        private const string EVENT_TIME_FORMAT = "ddd, MMM dd @ h:mmtt";
        private const string DAY_STRING = "day";
        private const string HOUR_STRING = "hour";
        private const string MINUTES_STRING = "min";

        public static string GetEventTimeText(IEventDTO eventDTO)
        {
            string schedule = string.Empty;

            if (!DateTime.TryParse(eventDTO.Start_at, null, DateTimeStyles.RoundtripKind, out DateTime startAt)) return schedule;

            if (eventDTO.Live)
            {
                TimeSpan elapsed = DateTime.UtcNow - startAt;

                if (elapsed.TotalDays >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalDays, DAY_STRING);
                else if (elapsed.TotalHours >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalHours, HOUR_STRING);
                else
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalMinutes, MINUTES_STRING);
            }
            else
            {
                DateTime localDateTime = startAt.ToLocalTime();
                schedule = localDateTime.ToString(EVENT_TIME_FORMAT).ToUpper();
            }

            return schedule;
        }
    }
}
