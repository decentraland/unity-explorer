using UnityEngine;

namespace Utility
{
    public static class PlatformUtils
    {
        private static string platformSuffix;

        public static string GetPlatform()
        {
            if (platformSuffix == null)
            {
                if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                    platformSuffix = "_windows";
                else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                    platformSuffix = "_mac";
                else if (Application.platform == RuntimePlatform.LinuxPlayer)
                    platformSuffix = "_linux";
                else
                    platformSuffix = string.Empty; // WebGL requires no platform suffix
            }

            return platformSuffix;
        }
    }
}
