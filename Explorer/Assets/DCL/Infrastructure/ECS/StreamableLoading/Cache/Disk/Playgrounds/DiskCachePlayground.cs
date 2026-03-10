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
        [SerializeField] private bool useCleanUp = true;


        private void Start()
        {
            StartAsync().Forget();
        }
        
        private void Update()
        {
            CleanUp();
        }

        private IDiskCache NewDiskCache(out IDiskCleanUp cleanUp)
        {
            var directory = CacheDirectory.New(cacheDirectory);
            var filesLock = new FilesLock();
            cleanUp = useCleanUp ? new LRUDiskCleanUp(directory, filesLock) : IDiskCleanUp.None.INSTANCE;
            return new DiskCache(directory, filesLock, cleanUp);
        }

        private async UniTaskVoid StartAsync()
        {
            byte[] testData = await File.ReadAllBytesAsync(testFile, destroyCancellationToken)!;
            string testExtension = Path.GetExtension(testFile);

            IDiskCache diskCache = NewDiskCache(out _);
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
            IDiskCache diskCache = NewDiskCache(out _);
            using HashKey hashKey = HashKey.FromString(testFile);
            var result = await diskCache.RemoveAsync(hashKey, Path.GetExtension(testFile), destroyCancellationToken);
            print($"Remove result: success {result.Success} and error {result.Error?.Message}");
        }

        [ContextMenu(nameof(CleanUp))]
        public void CleanUp()
        {
            var _ = NewDiskCache(out var cache);
            cache.CleanUpIfNeeded();
        }
    }
}
