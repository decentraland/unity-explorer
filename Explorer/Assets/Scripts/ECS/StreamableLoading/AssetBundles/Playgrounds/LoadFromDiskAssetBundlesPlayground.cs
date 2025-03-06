using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.Optimization.Memory;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Playgrounds
{
    public class FromDiskAssetBundlesPlayground : MonoBehaviour
    {
        [SerializeField] private bool exactPath;
        [SerializeField] private string dirPath;
        [Space]
        [SerializeField] private bool withMemoryChain;
        [SerializeField] private int byIndex;

        [ContextMenu(nameof(StartAsync))]
        public async UniTaskVoid StartAsync()
        {
            foreach (string file in Directory.EnumerateFiles(dirPath))
            {
                print($"Load file successfully: {file}");
                await using var fs = new FileStream(file, FileMode.Open);

                // 5 is meta
                fs.Seek(PartialDiskSerializer.Meta.META_SIZE, SeekOrigin.Begin);
                using var stream = NewStream(fs);

                var ab = AssetBundle.LoadFromStream(stream);
                if (ab) ab.Unload(true);
            }
        }

        private (IDiskCache<PartialLoadingState> diskCache, FilesLock filesLock, CacheDirectory cacheDirectory) New()
        {
            var dir = exactPath ? CacheDirectory.NewExact(dirPath) : CacheDirectory.New(dirPath);
            var filesLock = new FilesLock();
            var cache = new DiskCache(dir, new FilesLock(), new LRUDiskCleanUp(dir, filesLock));
            IDiskCache<PartialLoadingState> diskCache = new DiskCache<PartialLoadingState, PartialDiskSerializer.PartialMemoryIterator>(cache, new PartialDiskSerializer());
            return (diskCache, filesLock, dir);
        }

        [ContextMenu(nameof(LoadByCacheAsync))]
        private async UniTaskVoid LoadByCacheAsync()
        {
            (IDiskCache<PartialLoadingState> diskCache, FilesLock filesLock, CacheDirectory directory) = New();

            foreach (string file in Directory.EnumerateFiles(directory.Path))
            {
                print($"Load file successfully: {file}");
                (HashKey hash, string ext) = HashNamings.UnpackedFromPath(file);
                var content = await diskCache.ContentAsync(hash, ext, CancellationToken.None);
                var result = content.Unwrap().Value;
                var chain = result.TransferMemoryOwnership();
                using var stream = chain.ToStream();

                if (result.IsFileFullyDownloaded == false)
                {
                    ReportHub.LogException(new Exception("Not fully loaded"), ReportData.UNSPECIFIED);
                    continue;
                }

                await UniTask.SwitchToMainThread();
                var ab = AssetBundle.LoadFromStream(stream);
                print($"Asset: {(ab ? ab.name : string.Empty)}");
                if (ab) ab.Unload(true);
            }
        }

        [ContextMenu(nameof(LoadByCacheSingleIndexAsync))]
        private async UniTaskVoid LoadByCacheSingleIndexAsync()
        {
            (IDiskCache<PartialLoadingState> diskCache, FilesLock filesLock, CacheDirectory directory) = New();

            string file = Directory.EnumerateFiles(directory.Path).ToList()[byIndex];

            {
                print($"Load file successfully: {file}");
                (HashKey hash, string ext) = HashNamings.UnpackedFromPath(file);
                var content = await diskCache.ContentAsync(hash, ext, CancellationToken.None);
                var result = content.Unwrap().Value;
                var chain = result.TransferMemoryOwnership();
                using var stream = chain.ToStream();

                if (result.IsFileFullyDownloaded == false)
                    throw new Exception("Not fully loaded");

                await UniTask.SwitchToMainThread();
                var ab = AssetBundle.LoadFromStream(stream);
                if (ab) ab.Unload(true);
            }

            byIndex++;
        }

        private Stream NewStream(FileStream fs)
        {
            var memory = new MemoryStream((int)(fs.Length - PartialDiskSerializer.Meta.META_SIZE));
            fs.CopyTo(memory);

            if (withMemoryChain)
            {
                var c = new MemoryChain(ISlabAllocator.SHARED);

                c.AppendData(memory.ToArray());
                memory.Dispose();
                return c.ToStream();
            }

            return memory;
        }
    }
}
