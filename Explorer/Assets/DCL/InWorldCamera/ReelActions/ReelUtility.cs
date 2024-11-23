using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;

namespace DCL.InWorldCamera.ReelActions
{
    public class ReelUtility
    {
        public static DateTime GetImageDateTime(CameraReelResponseCompact image) =>
            GetDateTimeFromString(image.dateTime);

        public static DateTime GetDateTimeFromString(string epochString)
        {
            DateTime actualDateTime = !long.TryParse(epochString, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1, 0, 0, 0, 0);
        }
    }
}
