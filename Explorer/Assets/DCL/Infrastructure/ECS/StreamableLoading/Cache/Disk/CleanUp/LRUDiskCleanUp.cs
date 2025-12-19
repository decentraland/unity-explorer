using ECS.StreamableLoading.Cache.Disk.Lock;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;

namespace ECS.StreamableLoading.Cache.Disk.CleanUp
{
    public class LRUDiskCleanUp : IDiskCleanUp
    {
        private readonly CacheDirectory cacheDirectory;
        private readonly FilesLock filesLock;
        private readonly long maxCacheSizeBytes;

        /// <summary>
        /// Enumerable that converts each file to its size in bytes.
        /// Used to compute total cache size without unnecessary string allocations.
        /// </summary>
        private FileSystemEnumerable<long> fileSizeEnumerable;

        /// <summary>
        /// Converts each file to a <see cref="CacheFileInfo"/>.
        /// </summary>
        private FileSystemEnumerable<CacheFileInfo> cacheFileInfoEnumerable;

        private Dictionary<int, string> fullPathCache = new ();

        public LRUDiskCleanUp(CacheDirectory cacheDirectory, FilesLock filesLock, long maxCacheSizeBytes = 1024 * 1024 * 1024) //1GB
        {
            this.cacheDirectory = cacheDirectory;
            this.filesLock = filesLock;
            this.maxCacheSizeBytes = maxCacheSizeBytes;

            CreateFileSystemEnumerables();
        }

        private void CreateFileSystemEnumerables()
        {
            var enumerationOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            fileSizeEnumerable = new FileSystemEnumerable<long>(cacheDirectory.Path, (ref FileSystemEntry entry) => entry.Length, enumerationOptions)
            {
                ShouldIncludePredicate = IgnoreDirectories,
            };

            cacheFileInfoEnumerable = new FileSystemEnumerable<CacheFileInfo>(
                cacheDirectory.Path,
                (ref FileSystemEntry entry) =>
                {
                    int fileId = GetFileId(entry.FileName);

                    // We cache and reuse path strings to avoid allocating them more than once.
                    if (!fullPathCache.TryGetValue(fileId, out string fullPath))
                    {
                        fullPath = entry.ToFullPath();
                        fullPathCache.Add(fileId, fullPath);
                    }

                    return new CacheFileInfo(fileId, fullPath, entry.Length, entry.LastAccessTimeUtc.DateTime);
                },
                enumerationOptions)
            {
                ShouldIncludePredicate = IgnoreDirectories,
            };

            return;

            bool IgnoreDirectories(ref FileSystemEntry entry) => !entry.IsDirectory;
        }

        /// <summary>
        /// Computes an ID for each file by hashing its file name.
        /// No need to hash the full path because all files are in the same directory and there is no recursion.
        /// </summary>
        private static int GetFileId(ReadOnlySpan<char> fileName)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < fileName.Length; i++)
                {
                    char c = fileName[i];
                    hash = (hash * 31) + c;
                }
                return hash;
            }
        }

        public void CleanUpIfNeeded()
        {
            Profiler.BeginSample("LRU Disk Clean Up - Clean Up If Needed");

            long cacheSize = ComputeCacheSize();
            if (cacheSize <= maxCacheSizeBytes)
            {
                Profiler.EndSample();
                return;
            }

            using var cachedFilesScope = CachedFiles(out var cachedFiles);
            cachedFiles.Sort(LRUComparer.INSTANCE);

            for (int i = 0; i < cachedFiles.Count; i++)
            {
                var file = cachedFiles[i];

                using var lockScope = filesLock.TryLock(file.FullPath, out bool isLocked);
                if (!isLocked) continue;

                File.Delete(file.FullPath);
                // Remove the entry from the lookup.
                // Not strictly necessary and could avoid extra allocs if the same file is cached again.
                // Still, we remove it to avoid the lookup growing indefinitely.
                fullPathCache.Remove(file.FileId);

                cacheSize -= file.Size;
                if (cacheSize < maxCacheSizeBytes) break;
            }

            Profiler.EndSample();
        }

        public void NotifyUsed(string fileName)
        {
            int fileId = GetFileId(fileName.AsSpan());

            if (!fullPathCache.TryGetValue(fileId, out string fullPath))
            {
                fullPath = Path.Combine(cacheDirectory.Path, fileName);
                fullPathCache.Add(fileId, fullPath);
            }

            File.SetLastAccessTimeUtc(fullPath, DateTime.UtcNow);
        }

        private long ComputeCacheSize()
        {
            long cacheSize = 0;
            foreach (long fileSize in fileSizeEnumerable) cacheSize += fileSize;
            return cacheSize;
        }

        private PooledObject<List<CacheFileInfo>> CachedFiles(out List<CacheFileInfo> list)
        {
            PooledObject<List<CacheFileInfo>> pooledObject = ListPool<CacheFileInfo>.Get(out list);
            foreach (CacheFileInfo cacheFileInfo in cacheFileInfoEnumerable) list.Add(cacheFileInfo);
            return pooledObject;
        }

        private readonly struct CacheFileInfo
        {
            public readonly int FileId;
            public readonly string FullPath;
            public readonly long Size;
            public readonly DateTime LastAccessTimeUtc;

            public CacheFileInfo(int fileId, string fullPath, long size, DateTime lastAccessTimeUtc)
            {
                FileId = fileId;
                FullPath = fullPath;
                Size = size;
                LastAccessTimeUtc = lastAccessTimeUtc;
            }

            public static CacheFileInfo FromFileSystemEntry(int fileId, ref FileSystemEntry entry) =>
                new (fileId, entry.ToFullPath()!, entry.Length, entry.LastAccessTimeUtc.DateTime);
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
