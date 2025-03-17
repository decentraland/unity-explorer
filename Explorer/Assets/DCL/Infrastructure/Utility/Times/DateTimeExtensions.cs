using System;

namespace Utility.Times
{
    public static class DateTimeExtensions
    {
        public static readonly DateTime UNIX_START = new (1970, 1, 1);

        public static int UnixTimeSeconds(this DateTime dateTime) =>
            (int)dateTime.Subtract(UNIX_START).TotalSeconds;

        public static ulong UnixTimeAsMilliseconds(this DateTime dateTime) =>
            (ulong)dateTime.Subtract(UNIX_START).TotalMilliseconds;
    }
}
