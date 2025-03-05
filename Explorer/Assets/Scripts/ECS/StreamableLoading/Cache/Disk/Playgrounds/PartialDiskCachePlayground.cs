using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.IO;
using UnityEngine;

namespace ECS.StreamableLoading.Cache.Disk.Playgrounds
{
    public class PartialDiskCachePlayground : MonoBehaviour
    {
        [SerializeField] private string cacheDirectory = string.Empty;
        [SerializeField] private Texture2D texture = null!;
        private const string TEST_FILE = "part.png";

        private void Start()
        {
            StartAsync().Forget();
        }

        private IDiskCache<PartialLoadingState> NewDiskCache()
        {
            var diskCache = new DiskCache(CacheDirectory.New(cacheDirectory), new FilesLock(), IDiskCleanUp.None.INSTANCE);
            return new DiskCache<PartialLoadingState, PartialDiskSerializer.PartialMemoryIterator>(diskCache, new PartialDiskSerializer());
        }

        private async UniTaskVoid StartAsync()
        {
            string testExtension = "part";

            IDiskCache<PartialLoadingState> diskCache = NewDiskCache();
            using HashKey hashKey = HashKey.FromString(TEST_FILE);

            var originData = texture.GetRawTextureData<byte>();
            var data = new PartialLoadingState(originData.Length, true);

            data.AppendData(originData.AsSpan());

            var result = await diskCache.PutAsync(hashKey, testExtension, data, destroyCancellationToken);
            print($"Put result: success {result.Success} and error {result.Error?.Message}");

            var contentResult = await diskCache.ContentAsync(hashKey, testExtension, destroyCancellationToken);
            print($"Content result: success {contentResult.Success} and error {contentResult.Error?.Message}");

            var gotten = contentResult.Value.Value;
            using var gottenStream = gotten.TransferMemoryOwnership().ToStream();

            var gottenData = new byte[gottenStream.Length];
            gottenStream.Read(gottenData, 0, gottenData.Length);

            print($"Meta is equal: {gotten.FullFileSize == data.FullFileSize} {gotten.IsFileFullyDownloaded == data.IsFileFullyDownloaded}");
            print($"Content equals: {originData.AsSpan().SequenceEqual(gottenData.AsSpan())}, origin: {originData.Length} gotten: {gottenData.Length}");
        }
    }
}
