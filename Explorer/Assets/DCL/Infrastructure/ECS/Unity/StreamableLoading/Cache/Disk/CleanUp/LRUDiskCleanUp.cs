using ECS.StreamableLoading.Cache.Disk.Lock; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache.Disk.CleanUp
{
    public class LRUDiskCleanUp : IDiskCleanUp
    {
        private static readonly EnumerationOptions options =  
            new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

        private readonly CacheDirectory cacheDirectory;
        private readonly FilesLock filesLock;
        private readonly long maxCacheSizeBytes;

        /// <summary>
        /// Use file system enumerable because default DirectoryInfo API allocates a lot of memory per each call
        /// </summary>
        private readonly FileSystemEnumerable<CacheFileInfo> files;
        private readonly FileSystemEnumerable<long> fileSizes;

        public LRUDiskCleanUp(CacheDirectory cacheDirectory, FilesLock filesLock, long maxCacheSizeBytes = 1024 * 1024 * 1024) //1GB
        {
            this.cacheDirectory = cacheDirectory;
            this.filesLock = filesLock;
            this.maxCacheSizeBytes = maxCacheSizeBytes;

            files = new FileSystemEnumerable<CacheFileInfo>(
                cacheDirectory.Path,
                (ref FileSystemEntry entry) => CacheFileInfo.FromFileSystemEntry(ref entry),
                options
            )
            {
                ShouldIncludePredicate = Predicate,
            };

            fileSizes = new FileSystemEnumerable<long>(
                cacheDirectory.Path,
                (ref FileSystemEntry entry) => entry.Length,
                options
            )
            {
                ShouldIncludePredicate = Predicate,
            };

            static bool Predicate(ref FileSystemEntry entry) => entry.IsDirectory == false;
        }

        public void CleanUpIfNeeded()
        {
            long directorySize = DirectorySize(fileSizes);

            if (directorySize > maxCacheSizeBytes)
            {
                using var _ = CachedFiles(out var cachedFiles);
                cachedFiles.Sort(LRUComparer.INSTANCE);

                for (int i = 0; i < cachedFiles.Count; i++)
                {
                    var file = cachedFiles[i];
                    using var lockScope = filesLock.TryLock(file.FullPath, out bool success);

                    if (success == false)
                        continue;

                    File.Delete(file.FullPath);
                    directorySize -= file.Size;
                    if (directorySize < maxCacheSizeBytes) break;
                }
            }
        }

        public void NotifyUsed(string fileName)
        {
            File.SetLastAccessTimeUtc(Path.Combine(cacheDirectory.Path, fileName), DateTime.UtcNow);
        }

        private static long DirectorySize(FileSystemEnumerable<long> fileSizes)
        {
            long size = 0;
            foreach (long fi in fileSizes) size += fi;
            return size;
        }

        private PooledObject<List<CacheFileInfo>> CachedFiles(out List<CacheFileInfo> list)
        {
            PooledObject<List<CacheFileInfo>> pooledObject = ListPool<CacheFileInfo>.Get(out list);
            list!.Clear();
            foreach (CacheFileInfo cacheFileInfo in files) list.Add(cacheFileInfo);
            return pooledObject;
        }

        private readonly struct CacheFileInfo
        {
            public readonly string FullPath;
            public readonly long Size;
            public readonly DateTime LastAccessTimeUtc;

            public CacheFileInfo(string fullPath, long size, DateTime lastAccessTimeUtc)
            {
                FullPath = fullPath;
                Size = size;
                LastAccessTimeUtc = lastAccessTimeUtc;
            }

            public static CacheFileInfo FromFileSystemEntry(ref FileSystemEntry entry) =>
                new (entry.ToFullPath()!, entry.Length, entry.LastAccessTimeUtc.DateTime);
        }

        private class LRUComparer : IComparer<CacheFileInfo>
        {
            public static readonly LRUComparer INSTANCE = new ();

            private LRUComparer() { }

            public int Compare(CacheFileInfo xInfo, CacheFileInfo yInfo) =>
                xInfo.LastAccessTimeUtc.CompareTo(yInfo.LastAccessTimeUtc);
        }
    }
}
