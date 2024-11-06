using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Profiles;
using ECS;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera
{
    public static class ScreenshotMetadataProcessor
    {
        public static ScreenshotMetadata Create(Profile profile, RealmData realm, Vector2Int playerPosition, string sceneName, VisiblePerson[] visiblePeople)
        {
            var metadata = new ScreenshotMetadata
            {
                userName = profile.Name,
                userAddress = profile.UserId,
                dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                realm = realm?.RealmName,
                scene = new Scene
                {
                    name = sceneName,
                    location = new Location(playerPosition),
                },
                visiblePeople = visiblePeople,
            };

            return metadata;
        }

        public static DateTime GetLocalizedDateTime(this ScreenshotMetadata screenshotMetadata)
        {
            if (!long.TryParse(screenshotMetadata.dateTime, out long unixTimestamp)) return new DateTime();
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
        }

        public static DateTime GetStartOfTheMonthDate(this ScreenshotMetadata screenshotMetadata)
        {
            DateTime localizedDateTime = GetLocalizedDateTime(screenshotMetadata);
            return new DateTime(localizedDateTime.Year, localizedDateTime.Month, 1);
        }
    }
}
