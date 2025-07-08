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
            if (string.IsNullOrEmpty(path))
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, "GetAvailableStorageBytes was called with a null or empty path.");
                return 0;
            }

            try
            {
                var drive = new DriveInfo(path);
                return drive.IsReady ? drive.AvailableFreeSpace : 0;
            }
            catch (ArgumentException ex)
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Could not determine available storage. The path '{path}' is invalid. Error: {ex.Message}");
                return 0;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"An unexpected error occurred while checking storage for path '{path}'. Error: {e.Message}");
                return 0;
            }
        }
    }
}