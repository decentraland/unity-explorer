using System;
using System.IO;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public static class PlatformUtils
    {
        public static PlatformOS DetectPlatform()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => PlatformOS.Windows,
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => PlatformOS.Mac,
                _ => PlatformOS.Unsupported
            };
        }

        public static long GetAvailableStorageBytes(string path)
        {
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                string root = Path.GetPathRoot(path);
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady &&
                        string.Equals(
                            drive.Name.Replace('\\', '/').TrimEnd('/'),
                            root.Replace('\\', '/').TrimEnd('/'),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return drive.AvailableFreeSpace;
                    }
                }

                return 0;

#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var drive = new DriveInfo(path);
            return drive.IsReady ? drive.AvailableFreeSpace : 0;
#else
            // Fallback for unsupported platforms
            return 0;
#endif
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Could not determine available storage space for path '{path}'. Error: {e.Message}");
                return 0;
            }
        }
    }
}