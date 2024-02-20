using System;

namespace Utility.Times
{
    public static class DateTimeExtensions
    {
        public static int UnixTime(this DateTime dateTime) =>
            (int)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
