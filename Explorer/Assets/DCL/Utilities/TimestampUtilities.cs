using System;
using System.Text;

namespace DCL.Utilities
{
    public static class TimestampUtilities
    {
        public static string GetRelativeTime(string timestampString)
        {
            //Using string builder to avoid boxing allocations
            StringBuilder sb = new StringBuilder();

            var timestamp = long.Parse(timestampString);

            TimeSpan timeDifference = DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;

            // Determine the appropriate string
            if (timeDifference.TotalSeconds < 60)
                return "less than a minute ago";

            if (timeDifference.TotalMinutes < 60)
            {
                var minutes = (int)Math.Floor(timeDifference.TotalMinutes);
                return FormatTime(sb, minutes, "minute");
            }

            if (timeDifference.TotalHours < 24)
            {
                var hours = (int)Math.Floor(timeDifference.TotalHours);
                return FormatTime(sb, hours, "hour");
            }

            if (timeDifference.TotalDays < 30)
            {
                var days = (int)Math.Floor(timeDifference.TotalDays);
                return FormatTime(sb, days, "day");
            }

            if (timeDifference.TotalDays < 365)
            {
                var months = (int)Math.Floor(timeDifference.TotalDays / 30);
                return FormatTime(sb, months, "month");
            }

            var years = (int)Math.Floor(timeDifference.TotalDays / 365);
            return FormatTime(sb, years, "year");
        }

        private static string FormatTime(StringBuilder sb, int value, string unit)
        {
            sb.Append(value);
            sb.Append(" ");
            sb.Append(unit);
            if (value > 1)
                sb.Append("s");
            sb.Append(" ago");
            return sb.ToString();
        }
    }
}
