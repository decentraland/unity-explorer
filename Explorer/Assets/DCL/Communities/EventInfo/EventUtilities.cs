using DCL.EventsApi;
using System;
using System.Text;

namespace DCL.Communities.EventInfo
{
    public static class EventUtilities
    {
        private const string STARTED_EVENT_TIME_FORMAT = "Started {0} {1} ago";
        private const string EVENT_TIME_FORMAT = "ddd, MMM dd @ h:mmtt";
        private const string DAY_STRING = "day";
        private const string HOUR_STRING = "hour";
        private const string MINUTES_STRING = "min";
        private const string JUMP_IN_GC_LINK = " https://decentraland.org/jump/?position={0},{1}";
        private const string JUMP_IN_WORLD_LINK = " https://decentraland.org/jump/?realm={0}";
        private const string EVENT_WEBSITE_LINK = "https://decentraland.org/events/event/?id={0}";
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_HASHTAG = "DCLPlace";

        public static string GetEventTimeText(IEventDTO eventDTO)
        {
            string schedule = string.Empty;

            if (eventDTO.StartAtProcessed == default(DateTime)) return schedule;

            if (eventDTO.Live)
            {
                TimeSpan elapsed = DateTime.UtcNow - eventDTO.StartAtProcessed;

                if (elapsed.TotalDays >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalDays, DAY_STRING);
                else if (elapsed.TotalHours >= 1)
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalHours, HOUR_STRING);
                else
                    schedule = string.Format(STARTED_EVENT_TIME_FORMAT, (int)elapsed.TotalMinutes, MINUTES_STRING);
            }
            else
            {
                DateTime localDateTime = eventDTO.StartAtProcessed.ToLocalTime();
                schedule = localDateTime.ToString(EVENT_TIME_FORMAT).ToUpper();
            }

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

            var day = localStart.ToString("dddd");
            sb.Append(char.ToUpper(day[0]));
            sb.Append(day, 1, day.Length - 1);
            sb.Append(", ");

            var month = localStart.ToString("MMM");
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

        public static Uri GetEventCopyLink(IEventDTO eventData) =>
            eventData.Live
                ? GetPlaceJumpInLink(eventData)
                : GetEventWebsiteLink(eventData);

        private static Uri GetPlaceJumpInLink(IEventDTO eventData) =>
            new (eventData.World ? string.Format(JUMP_IN_WORLD_LINK, eventData.Server) : string.Format(JUMP_IN_GC_LINK, eventData.X, eventData.Y));

        private static Uri GetEventWebsiteLink(IEventDTO eventData) =>
            new (string.Format(EVENT_WEBSITE_LINK, eventData.Id));

        public static Uri GetEventShareLink(IEventDTO eventData) =>
            new (string.Format(TWITTER_NEW_POST_LINK, eventData.Name, TWITTER_HASHTAG, GetEventCopyLink(eventData)));
    }
}
