using DCL.Diagnostics;
using System.IO;
using UnityEngine;

namespace ECS.StreamableLoading.Cache.Disk
{
    public readonly struct CacheDirectory
    {
        //Bump this version if there is the need to wipe out the old disk cache when introducing major changes to DiskCaching
        private const string CACHE_VERSION = "V1";
        private static readonly string DISK_CACHE_FOLDER = $"DiskCache{CACHE_VERSION}";
        public readonly string Path;

        private CacheDirectory(string path)
        {
            this.Path = path;
        }

        public static CacheDirectory New(string subdirectory)
        {
            string dirPath = System.IO.Path.Combine(Application.persistentDataPath!, subdirectory);

            // Clean up old cache directories
            if (subdirectory.StartsWith("DiskCache"))
            {
                string baseDir = Application.persistentDataPath!;
                string[] existingDirs = Directory.GetDirectories(baseDir, "DiskCache*");
                string currentVersionFolder = System.IO.Path.Combine(baseDir, DISK_CACHE_FOLDER);

                foreach (string dir in existingDirs)
                {
                    // Different version folders
                    if (dir != currentVersionFolder && dir.StartsWith(System.IO.Path.Combine(baseDir, "DiskCache")))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            ReportHub.Log(ReportCategory.DEBUG, $"DiskCache: deleted old cache directory at {dir}");
                        }
                        catch (IOException ex)
                        {
                            ReportHub.Log(ReportCategory.DEBUG, $"DiskCache: failed to delete old cache directory at {dir}: {ex.Message}");
                        }
                    }
                }
            }

            if (Directory.Exists(dirPath) == false) Directory.CreateDirectory(dirPath);
            ReportHub.Log(ReportCategory.DEBUG, $"DiskCache: use directory at {dirPath}");
            return new CacheDirectory(dirPath);
        }

        public static CacheDirectory NewDefault() =>
            New($"{DISK_CACHE_FOLDER}");

        public static CacheDirectory NewDefaultSubdirectory(string subdirectory) =>
            New($"{DISK_CACHE_FOLDER}/{subdirectory}");

    }
}
