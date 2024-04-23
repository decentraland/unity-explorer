using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class ResolutionUtils
    {
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
