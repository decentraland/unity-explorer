using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plugins.NativeWindowManager
{
    internal static class ResolutionUtils
    {
        public static List<Vector2Int> GetAvailableResolutions()
        {
            List<Vector2Int> resolutions = new ();

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
                if (resolutions.Any(res => res.x == resolution.height
                                           && res.y == resolution.width))
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
                resolutions.Add(new Vector2Int(resolution.width, resolution.height));
            }
        }

        public static Vector2Int GetDefaultResolution()
        {
            var possibleResolutions = GetAvailableResolutions();

            int defaultIndex = 0;

            for (var index = 0; index < possibleResolutions.Count; index++)
            {
                var resolution = possibleResolutions[index];

                if (!IsDefaultResolution(resolution))
                    continue;

                defaultIndex = index;
                break;
            }

            var defaultResolution = possibleResolutions[defaultIndex];

            return new Vector2Int(defaultResolution.x, defaultResolution.y);
        }

        private static bool IsResolutionCompatibleWithAspectRatio(float resolutionWidth, float resolutionHeight, float aspectRatioWidth, float aspectRatioHeight)
        {
            const float EPSILON = 0.0001f;
            float resolutionAspectRatioValue = GetAspectRatioValue(resolutionWidth, resolutionHeight);
            float aspectRationValueToCheck = GetAspectRatioValue(aspectRatioWidth, aspectRatioHeight);
            return Mathf.Abs(resolutionAspectRatioValue - aspectRationValueToCheck) <= EPSILON;
        }

        // By design, the default resolution should be a 1080p resolution with a 16:9 aspect ratio
        public static bool IsDefaultResolution(Vector2Int resolution) =>
            resolution.y == 1080 && IsResolutionCompatibleWithAspectRatio(resolution.x, resolution.y, 16.0f, 9.0f);

        private static float GetAspectRatioValue(float width, float height) =>
            width / height;
    }
}
