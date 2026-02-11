using DCL.EventsApi;
using System;
using System.Globalization;
using System.Text;

namespace DCL.Communities.EventInfo
{
    public static class EventUtilities
    {
        private const string STARTED_EVENT_TIME_FORMAT = "Started {0} {1} ago";
        private const string EVENT_TIME_FORMAT = "ddd, MMM dd @ h:mmtt";
        private const string EVENT_TIME_FORMAT_ONLY_HOURS = "h:mmtt";
        private const string EVENT_DAY_FORMAT = "ddd, MMM dd";
        private const string DAY_STRING = "day";
        private const string HOUR_STRING = "hour";
        private const string MINUTES_STRING = "min";
        private const string JUMP_IN_GC_LINK = " https://decentraland.org/jump/?position={0},{1}";
        private const string JUMP_IN_WORLD_LINK = " https://decentraland.org/jump/?realm={0}";
        private const string EVENT_WEBSITE_LINK = "https://decentraland.org/events/event/?id={0}";
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_HASHTAG = "DCLPlace";
        private const string ADD_TO_CALENDAR_LINK = "https://calendar.google.com/calendar/r/eventedit?text={0}&details={1}\n\n{2}&dates={3}/{4}";

        public static string GetEventTimeText(IEventDTO eventDTO, bool showOnlyHoursFormat = false)
        {
            string schedule = string.Empty;

            if (eventDTO.NextStartAtProcessed == default(DateTime)) return schedule;

            if (eventDTO.Live)
            {
                TimeSpan elapsed = DateTime.UtcNow - eventDTO.NextStartAtProcessed;

                if (elapsed.TotalDays >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalDays, DAY_STRING);
                else if (elapsed.TotalHours >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalHours, HOUR_STRING);
                else
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalMinutes, MINUTES_STRING);
            }
            else
            {
                DateTime localDateTime = eventDTO.NextStartAtProcessed.ToLocalTime();
                schedule = localDateTime.ToString(showOnlyHoursFormat ? EVENT_TIME_FORMAT_ONLY_HOURS : EVENT_TIME_FORMAT, CultureInfo.InvariantCulture).ToUpper();
            }

            return schedule;
        }

        public static string GetEventDayText(IEventDTO eventDTO)
        {
            string schedule = string.Empty;

            if (eventDTO.NextStartAtProcessed == default(DateTime)) return schedule;

            DateTime localDateTime = eventDTO.NextStartAtProcessed.ToLocalTime();
            schedule = localDateTime.ToString(EVENT_DAY_FORMAT, CultureInfo.InvariantCulture).ToUpper();

            return schedule;
        }

        public static void FormatEventString(DateTime utcStart, double durationMs, StringBuilder sb)
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
            DateTime utcEnd = utcStart.Add(duration);

            TimeZoneInfo localZone = TimeZoneInfo.Local;
            DateTime localStart = TimeZoneInfo.ConvertTimeFromUtc(utcStart, localZone);
            DateTime localEnd = TimeZoneInfo.ConvertTimeFromUtc(utcEnd, localZone);

            TimeSpan offset = localZone.GetUtcOffset(localStart);

            var day = localStart.ToString("dddd", CultureInfo.InvariantCulture);
            sb.Append(char.ToUpper(day[0]));
            sb.Append(day, 1, day.Length - 1);
            sb.Append(", ");

            var month = localStart.ToString("MMM", CultureInfo.InvariantCulture);
            sb.Append(char.ToUpper(month[0]));
            sb.Append(month, 1, month.Length - 1);
            sb.Append(' ');
            sb.Append(localStart.ToString("dd"));
            sb.Append(" from ");

            sb.Append(localStart.ToString("hh:mmtt").ToLowerInvariant());
            sb.Append(" to ");
            sb.Append(localEnd.ToString("hh:mmtt").ToLowerInvariant());

            sb.Append(" (UTC");
            double offsetHours = offset.TotalHours;
            if (offsetHours >= 0)
                sb.Append('+');
            sb.Append(((int)offsetHours).ToString());
            sb.Append(')');
        }

        public static string GetEventCopyLink(IEventDTO eventData) =>
            eventData.Live
                ? GetPlaceJumpInLink(eventData)
                : GetEventWebsiteLink(eventData);

        private static string GetPlaceJumpInLink(IEventDTO eventData) =>
            eventData.World ? string.Format(JUMP_IN_WORLD_LINK, eventData.Server) : string.Format(JUMP_IN_GC_LINK, eventData.X, eventData.Y);

        private static string GetEventWebsiteLink(IEventDTO eventData) =>
            string.Format(EVENT_WEBSITE_LINK, eventData.Id);

        public static string GetEventShareLink(IEventDTO eventData) =>
            string.Format(TWITTER_NEW_POST_LINK, eventData.Name, TWITTER_HASHTAG, GetEventCopyLink(eventData));

        public static string GetEventAddToCalendarLink(IEventDTO eventData)
        {
            DateTime nextStartAtDate = DateTime.Parse(
                eventData.Next_start_at,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal
            );

            DateTime nextFinishAtDate = DateTime.Parse(
                eventData.Next_finish_at,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal
            );

            return string.Format(ADD_TO_CALENDAR_LINK,
                eventData.Name,
                eventData.Description,
                $"jump in: {GetPlaceJumpInLink(eventData)}",
                nextStartAtDate.ToString("yyyyMMdd'T'HHmmss'Z'"),
                nextFinishAtDate.ToString("yyyyMMdd'T'HHmmss'Z'"));
        }

        public static string GetEventAddToCalendarLink(IEventDTO eventData, DateTime utcStart)
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(eventData.Duration);
            DateTime utcEnd = utcStart.Add(duration);

            TimeZoneInfo localZone = TimeZoneInfo.Local;
            DateTime localStart = TimeZoneInfo.ConvertTimeFromUtc(utcStart, localZone);
            DateTime localEnd = TimeZoneInfo.ConvertTimeFromUtc(utcEnd, localZone);

            return string.Format(ADD_TO_CALENDAR_LINK,
                eventData.Name,
                eventData.Description,
                $"jump in: {GetPlaceJumpInLink(eventData)}",
                utcStart.ToString("yyyyMMdd'T'HHmmss'Z'"),
                utcEnd.ToString("yyyyMMdd'T'HHmmss'Z'"));
        }
    }
}
