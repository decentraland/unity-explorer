using Cysharp.Threading.Tasks;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Textures;
using System;
using System.IO;
using UnityEngine;

namespace ECS.StreamableLoading.Cache.Disk.Playgrounds
{
    public class TexturesDiskCachePlayground : MonoBehaviour
    {
        [SerializeField] private string cacheDirectory = string.Empty;
        [SerializeField] private Texture2D texture = null!;
        private const string TEST_FILE = "texture.png";

        private void Start()
        {
            StartAsync().Forget();
        }

        private IDiskCache<Texture2DData> NewDiskCache()
        {
            var diskCache = new DiskCache(CacheDirectory.New(cacheDirectory), new FilesLock(), IDiskCleanUp.None.INSTANCE);
            return new DiskCache<Texture2DData, SerializeMemoryIterator<TextureDiskSerializer.State>>(diskCache, new TextureDiskSerializer());
        }

        private async UniTaskVoid StartAsync()
        {
            string testExtension = "png";

            IDiskCache<Texture2DData> diskCache = NewDiskCache();
            using HashKey hashKey = HashKey.FromString(TEST_FILE);

            var data = new Texture2DData(texture);

            var result = await diskCache.PutAsync(hashKey, testExtension, data, destroyCancellationToken);
            print($"Put result: success {result.Success} and error {result.Error?.Message}");

            var contentResult = await diskCache.ContentAsync(hashKey, testExtension, destroyCancellationToken);
            print($"Content result: success {contentResult.Success} and error {contentResult.Error?.Message}");

            var originData = texture.GetRawTextureData<byte>();
            var gottenData = contentResult.Value.Value.Asset.GetRawTextureData<byte>();

            print($"Content equals: {originData.AsSpan().SequenceEqual(gottenData.AsSpan())}");
        }

        [ContextMenu(nameof(RemoveAsync))]
        public async UniTaskVoid RemoveAsync()
        {
            IDiskCache<Texture2DData> diskCache = NewDiskCache();
            using HashKey hashKey = HashKey.FromString(TEST_FILE);
            var result = await diskCache.RemoveAsync(hashKey, Path.GetExtension(TEST_FILE), destroyCancellationToken);
            print($"Remove result: success {result.Success} and error {result.Error?.Message}");
        }
    }
}
