using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using System;
using System.IO;
using UnityEngine;

namespace ECS.StreamableLoading.Cache.Disk.Playgrounds
{
    public class DiskCachePlayground : MonoBehaviour
    {
        [SerializeField] private string cacheDirectory = string.Empty;
        [SerializeField] private string testFile = string.Empty;

        private void Start()
        {
            StartAsync().Forget();
        }

        private IDiskCache NewDiskCache() =>
            new DiskCache(CacheDirectory.New(cacheDirectory), new FilesLock(), IDiskCleanUp.None.INSTANCE);

        private async UniTaskVoid StartAsync()
        {
            byte[] testData = await File.ReadAllBytesAsync(testFile, destroyCancellationToken)!;
            string testExtension = Path.GetExtension(testFile);

            IDiskCache diskCache = NewDiskCache();
            using HashKey hashKey = HashKey.FromString(testFile);

            using SingleMemoryIterator iterator = new SingleMemoryIterator(testData);

            var result = await diskCache.PutAsync(hashKey, testExtension, iterator, destroyCancellationToken);
            print($"Put result: success {result.Success} and error {result.Error?.Message}");

            var contentResult = await diskCache.ContentAsync(hashKey, testExtension, destroyCancellationToken);
            print($"Content result: success {contentResult.Success} and error {contentResult.Error?.Message}");

            print($"Content equals: {testData.AsSpan().SequenceEqual(contentResult.Value!.Value.Memory.Span)}");
        }

        [ContextMenu(nameof(RemoveAsync))]
        public async UniTaskVoid RemoveAsync()
        {
            IDiskCache diskCache = NewDiskCache();
            using HashKey hashKey = HashKey.FromString(testFile);
            var result = await diskCache.RemoveAsync(hashKey, Path.GetExtension(testFile), destroyCancellationToken);
            print($"Remove result: success {result.Success} and error {result.Error?.Message}");
        }
    }
}
