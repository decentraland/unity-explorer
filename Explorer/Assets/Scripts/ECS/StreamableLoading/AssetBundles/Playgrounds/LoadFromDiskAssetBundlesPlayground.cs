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

        private (IPartialDiskCache diskCache, FilesLock filesLock, CacheDirectory cacheDirectory) New()
        {
            var dir = exactPath ? CacheDirectory.NewExact(dirPath) : CacheDirectory.New(dirPath);
            var filesLock = new FilesLock();
            IPartialDiskCache diskCache = new PartialDiskCache(dir, filesLock, new LRUDiskCleanUp(dir, filesLock));
            return (diskCache, filesLock, dir);
        }

        [ContextMenu(nameof(LoadByCacheAsync))]
        private async UniTaskVoid LoadByCacheAsync()
        {
            (IPartialDiskCache diskCache, FilesLock filesLock, CacheDirectory directory) = New();

            foreach (string file in Directory.EnumerateFiles(directory.Path))
            {
                // print($"Load file successfully: {file}");
                // (HashKey hash, string ext) = HashNamings.UnpackedFromPath(file);
                // var content = await diskCache.PartialFileAsync(hash, ext, CancellationToken.None);
                // var result = content.Unwrap();
                // using var stream = chain.ToStream();
                //
                // if (result.MetaData.IsFullyDownloaded == false)
                // {
                //     ReportHub.LogException(new Exception("Not fully loaded"), ReportData.UNSPECIFIED);
                //     continue;
                // }
                //
                // await UniTask.SwitchToMainThread();
                // var ab = AssetBundle.LoadFromStream(stream);
                // print($"Asset: {(ab ? ab.name : string.Empty)}");
                // if (ab) ab.Unload(true);
            }
        }

        [ContextMenu(nameof(LoadByCacheSingleIndexAsync))]
        private async UniTaskVoid LoadByCacheSingleIndexAsync()
        {
            (IPartialDiskCache diskCache, FilesLock filesLock, CacheDirectory directory) = New();

            string file = Directory.EnumerateFiles(directory.Path).ToList()[byIndex];

            {
                // print($"Load file successfully: {file}");
                // (HashKey hash, string ext) = HashNamings.UnpackedFromPath(file);
                // var content = await diskCache.PartialFileAsync(hash, ext, CancellationToken.None);
                // var result = content.Unwrap();
                // using var stream = partialFile.ToStream();
                //
                // if (result.MetaData.IsFullyDownloaded == false)
                //     throw new Exception("Not fully loaded");
                //
                // await UniTask.SwitchToMainThread();
                // var ab = AssetBundle.LoadFromStream(stream);
                // if (ab) ab.Unload(true);
            }

            byIndex++;
        }
    }
}
