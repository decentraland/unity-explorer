using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;

namespace DCL.InWorldCamera.ReelActions
{
    public static class ReelUtility
    {
        /// <summary>
        ///     Get the DateTime from a CameraReelResponseCompact.
        ///     The DateTime is the actual year/month of the reel but collapsed to the first day of the month as it used to create reel "buckets".
        /// </summary>
        public static DateTime GetImageDateTime(CameraReelResponseCompact image) =>
            GetMonthDateTimeFromString(image.dateTime);

        /// <summary>
        ///     Extracts a DateTime from an epoch string.
        /// </summary>
        public static DateTime GetDateTimeFromString(string epochString) =>
            !long.TryParse(epochString, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;

        /// <summary>
        ///     Extracts a DateTime from an epoch string and collapses it to the first day of the month.
        /// </summary>
        public static DateTime GetMonthDateTimeFromString(string epochString)
        {
            DateTime actualDateTime = !long.TryParse(epochString, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1, 0, 0, 0, 0);
        }
    }
}
