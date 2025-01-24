using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
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
            new DiskCache(CacheDirectory.New(cacheDirectory), IDiskCleanUp.None.INSTANCE);

        private async UniTaskVoid StartAsync()
        {
            byte[] testData = await File.ReadAllBytesAsync(testFile, destroyCancellationToken)!;
            string testExtension = Path.GetExtension(testFile);

            IDiskCache diskCache = NewDiskCache();
            var result = await diskCache.PutAsync(testFile, testExtension, testData, destroyCancellationToken);
            print($"Put result: success {result.Success} and error {result.Error?.Message}");

            var contentResult = await diskCache.ContentAsync(testFile, testExtension, destroyCancellationToken);
            print($"Content result: success {contentResult.Success} and error {contentResult.Error?.Message}");

            print($"Content equals: {testData.AsSpan().SequenceEqual(contentResult.Value!.Value.Memory.Span)}");
        }

        [ContextMenu(nameof(RemoveAsync))]
        public async UniTaskVoid RemoveAsync()
        {
            IDiskCache diskCache = NewDiskCache();
            var result = await diskCache.RemoveAsync(testFile, Path.GetExtension(testFile), destroyCancellationToken);
            print($"Remove result: success {result.Success} and error {result.Error?.Message}");
        }
    }
}
