using System;
using System.IO;
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
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            var drive = new DriveInfo(path);
            if (drive.IsReady &&
                string.Equals(
                    drive.Name.Replace('\\', '/').TrimEnd('/'),
                    root.Replace('\\', '/').TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase))
            {
                return drive.AvailableFreeSpace;
            }
#endif
            return 0;
        }
    }
}