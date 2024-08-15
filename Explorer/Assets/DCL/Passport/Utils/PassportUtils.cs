using System;

namespace DCL.Passport.Utils
{
    public static class PassportUtils
    {
        public static string FormatTimestampDate(string timestampString)
        {
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestampString)).DateTime;
            var formattedDate = date.ToString("MMM. yyyy", System.Globalization.CultureInfo.InvariantCulture);
            return formattedDate;
        }
    }
}
