#if UNITY_EDITOR

using DCL.Diagnostics;
using System.IO;
using UnityEditor;

namespace ECS.StreamableLoading.Cache.Disk.Editor
{
    public static class DiskCacheMenu
    {
        [MenuItem("Decentraland/Cache/Clear Disk Cache")]
        public static void ClearDiskCache()
        {
            var directory = CacheDirectory.NewDefault();
            Directory.Delete(directory.Path, true);
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"Disk cache cleared at {directory.Path}");
        }
    }
}

#endif
