using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class ResolutionUtils
    {
        public static List<Resolution> GetAvailableResolutions() {

            List<Resolution> resolutions = new ();

            for (int index = Screen.resolutions.Length - 1; index >= 0; index--)
            {
                Resolution resolution = Screen.resolutions[index];

                // Exclude all resolutions that are not 16:9 or 16:10
                if (!IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16, 9) &&
                    !IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16, 10) &&

                    //Check for vertical monitors as well
                    !IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 9, 16) &&
                    !IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 10, 16))
                    continue;

                // Exclude all resolutions width less than 1024 (same for height in case of vertical monitors)
                if (Mathf.Min(resolution.width, resolution.height) <= 1024)
                    continue;

                // Exclude possible duplicates
                // Equals is not defined in Resolution class. LINQ used only in constructor to mimic a custom Equals
                if (resolutions.Any(res => res.height == resolution.height
                                           && res.width == resolution.width
                                           && ((int)Math.Round(res.refreshRateRatio.value)).Equals((int)Math.Round(resolution.refreshRateRatio.value))))
                    continue;

                AddResolution(resolution);
            }

            //Adds a fallback resolution if no other resolution is available
            if (resolutions.Count == 0)
            {
                var resolution = new Resolution
                {
                    width = 1920,
                    height = 1080
                };

                AddResolution(resolution);
            }

            return resolutions;

            void AddResolution(Resolution resolution)
            {
                resolutions.Add(resolution);
            }
        }

        public static bool IsResolutionCompatibleWithAspectRatio(float resolutionWidth, float resolutionHeight, float aspectRatioWidth, float aspectRatioHeight)
        {
            const float EPSILON = 0.0001f;
            float resolutionAspectRatioValue = GetAspectRatioValue(resolutionWidth, resolutionHeight);
            float aspectRationValueToCheck = GetAspectRatioValue(aspectRatioWidth, aspectRatioHeight);
            return Mathf.Abs(resolutionAspectRatioValue - aspectRationValueToCheck) <= EPSILON;
        }

        public static string FormatResolutionDropdownOption(Resolution resolution)
        {
            int width = resolution.width;
            int height = resolution.height;

            int tempWidth = width;
            int tempHeight = height;

            while (height != 0)
            {
                int rest = width % height;
                width = height;
                height = rest;
            }

            var aspectRationString = $"{tempWidth / width}:{tempHeight / width}";
            return $"{resolution.width}x{resolution.height} ({aspectRationString}) {resolution.refreshRate} Hz";
        }

        // By design, the default resolution should be a 1080p resolution with a 16:9 aspect ratio
        public static bool IsDefaultResolution(Resolution resolution) =>
            resolution.height == 1080 && IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16.0f, 9.0f);

        private static float GetAspectRatioValue(float width, float height) =>
            width / height;
    }
}
