using DCL.Diagnostics;
using System.IO;
using UnityEngine;

namespace ECS.StreamableLoading.Cache.Disk
{
    public readonly struct CacheDirectory
    {
        public readonly string Path;

        private CacheDirectory(string path)
        {
            this.Path = path;
        }

        public static CacheDirectory New(string subdirectory)
        {
            string dirPath = System.IO.Path.Combine(Application.persistentDataPath!, subdirectory);
            if (Directory.Exists(dirPath) == false) Directory.CreateDirectory(dirPath);
            ReportHub.Log(ReportCategory.DEBUG, $"DiskCache: use directory at {dirPath}");
            return new CacheDirectory(dirPath);
        }

        public static CacheDirectory NewDefault() =>
            New("DiskCache");

        public static CacheDirectory NewDefaultSubdirectory(string subdirectory) =>
            New("DiskCache/" + subdirectory);
    }
}
