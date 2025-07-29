using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using NUnit.Framework;
using SceneRuntime.Factory.JsSource;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Pool;
using Utility.Types;

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
            using var result = serializer.Serialize(data);

            var list = ListPool<byte[]>.Get();

            while (result.MoveNext())
                list.Add(result.Current.ToArray());

            var output = new List<byte>();

            foreach (byte[] bytes in list)
                output.AddRange(bytes);

            unsafe
            {
                fixed (char* str = data)
                {
                    byte* p = (byte*)str;

                    for (int i = 0; i < output.Count; i++)
                        Assert.AreEqual(p[i], output[i], $"Bytes at {i} are not same {p[i]} and {output[i]}");
                }
            }

            var slicedOwnedMemory = new SlicedOwnedMemory<byte>(output.Count);

            for (int i = 0; i < output.Count; i++)
                slicedOwnedMemory.Memory.Span[i] = output[i];

            string deserializedResult = await serializer.DeserializeAsync(slicedOwnedMemory, token);
            
            // Assert
            Assert.AreEqual(data, deserializedResult);
        }

        private class Owner : IMemoryOwner<byte>
        {
            public Owner(Memory<byte> memory)
            {
                Memory = memory;
            }

            public void Dispose()
            {
                //ignore
            }

            public Memory<byte> Memory { get; }
        }

        [Test]
        public async Task Cache()
        {
            // Arrange
            var diskCache = new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(new DiskCache(CacheDirectory.New("Test"), new FilesLock(), IDiskCleanUp.None.INSTANCE), new StringDiskSerializer());
            var key = HashKey.FromString("https://decentraland.org/images/ui/dark-atlas-v3.png");
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
