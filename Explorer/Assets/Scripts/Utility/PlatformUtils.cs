using UnityEngine;

namespace Utility
{
    public static class PlatformUtils
    {
        private static string? platformSuffix;

        public static string GetCurrentPlatform()
        {
            if (platformSuffix == null)
            {
                if (Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer)
                    platformSuffix = "_windows";
                else if (Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer)
                    platformSuffix = "_mac";
                else if (Application.platform is RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer)
                    platformSuffix = "_linux";
                else
                    platformSuffix = string.Empty; // WebGL requires no platform suffix
            }

            return platformSuffix;
        }
    }
}
