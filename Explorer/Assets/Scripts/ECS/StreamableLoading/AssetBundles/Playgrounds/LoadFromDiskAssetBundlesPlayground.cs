using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using DCL.Optimization.Memory;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.IO;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Playgrounds
{
    public class FromDiskAssetBundlesPlayground : MonoBehaviour
    {
        [SerializeField] private string dirPath;
        [SerializeField] private bool withMemoryChain;

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

        [ContextMenu(nameof(LoadByCacheAsync))]
        private async UniTaskVoid LoadByCacheAsync()
        {
            var dir = CacheDirectory.NewExact(dirPath);
            var filesLock = new FilesLock();
            var cache = new DiskCache(dir, new FilesLock(), new LRUDiskCleanUp(dir, filesLock));
            IDiskCache<PartialLoadingState> diskCache = new DiskCache<PartialLoadingState, PartialDiskSerializer.PartialMemoryIterator>(cache, new PartialDiskSerializer());

            foreach (string file in Directory.EnumerateFiles(dirPath))
            {
                print($"Load file successfully: {file}");
                (HashKey hash, string ext) = HashNamings.UnpackedFromPath(file);
                var content = await diskCache.ContentAsync(hash, ext, CancellationToken.None);
                var result = content.Unwrap().Value;
                var chain = result.TransferMemoryOwnership();
                using var stream = chain.ToStream();

                await UniTask.SwitchToMainThread();
                var ab = AssetBundle.LoadFromStream(stream);
                if (ab) ab.Unload(true);
            }
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
