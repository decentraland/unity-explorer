using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using NUnit.Framework;
using SceneRuntime.Factory.JsSource;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Factory.Tests
{
    public class JsCodeDiskSerializerShould
    {
        [Test]
        public async Task Serialize()
        {
            // Arrange
            var serializer = new StringDiskSerializer();
            var data = "Test data string";
            var token = new CancellationToken();

            // Act
            var result = await serializer.SerializeAsync(data, token);
            string deserialized = await serializer.DeserializeAsync(result, token);

            // Assert
            Assert.AreEqual(data, deserialized);
        }

        [Test]
        public async Task Cache()
        {
            // Arrange
            var diskCache = new DiskCache<string>(new DiskCache(CacheDirectory.New("Test"), IDiskCleanUp.None.INSTANCE), new StringDiskSerializer());
            var key = HashKey.FromOwnedMemory(OwnedMemory.FromString("https://decentraland.org/images/ui/dark-atlas-v3.png"));
            const string DATA = "Test data string";
            const string EXTENSION = "txt";
            var token = new CancellationToken();

            // Act
            var result = await diskCache.PutAsync(key, EXTENSION, DATA, token);

            if (result.Success == false)
                throw new System.Exception($"CachedWebJsSources: SceneSourceCodeAsync: diskCache.PutAsync failed: {result.Error!.Value.State} {result.Error!.Value.Message}");

            var contentResult = await diskCache.ContentAsync(key, EXTENSION, token);

            // Assert
            Assert.AreEqual(DATA, contentResult.Unwrap().Value);
        }
    }
}
