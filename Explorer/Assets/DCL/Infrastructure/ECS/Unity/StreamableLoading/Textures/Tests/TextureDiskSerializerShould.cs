using Cysharp.Threading.Tasks;
using DCL.Diagnostics.Tests;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    /// <summary>
    ///     Guards the disk-cache deserialization fast path: the serialized blob is
    ///     <c>GetRawTextureData&lt;byte&gt;()</c> (the full mip chain), so on a disk-cache hit the deserializer must
    ///     keep those real serialized mips instead of box-filter-regenerating mip 1..N from mip 0 (an O(W*H) native
    ///     pyramid pass on every hit).
    /// </summary>
    [TestFixture]
    public class TextureDiskSerializerShould
    {
        private const string EXTENSION = "tex";

        private MockedReportScope mockedReportScope = null!;
        private string directoryPath = null!;
        private DiskCache diskCache = null!;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();
            var directory = CacheDirectory.New($"TestTextureDiskCache-{Guid.NewGuid():N}");
            directoryPath = directory.Path;
            diskCache = new DiskCache(directory, new FilesLock(), IDiskCleanUp.None.INSTANCE);
        }

        [TearDown]
        public void TearDown()
        {
            mockedReportScope.Dispose();

            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
        }

        // [Performance] Regression guard: exactly the SERIALIZED mip 1 survives a round trip — the native
        // mip-pyramid regeneration pass (parameterless Apply(), updateMipmaps:true) must NOT run on a cache hit.
        // Red on revert: reverting to texture.Apply() box-filters mip 0 (black) over mip 1, so the red sentinel is
        // lost and every pixel of the deserialized mip 1 mismatches (a full W*H recompute per deserialize).
        [Test]
        public async Task PreserveSerializedMipsInsteadOfRegenerating()
        {
            var typedCache = new DiskCache<TextureData, SerializeMemoryIterator<TextureDiskSerializer.State>>(diskCache, new TextureDiskSerializer());

            const int SIZE = 64;
            const int MIP1_SIZE = SIZE / 2;

            var source = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, mipChain: true, linear: true);

            Assert.That(source.mipmapCount, Is.GreaterThan(1), "test needs a real mip chain");

            // mip 0 = black, mip 1 = a sentinel (red) that a box-filter of an all-black mip 0 can never produce.
            source.SetPixels(Filled(SIZE * SIZE, Color.black), 0);
            source.SetPixels(Filled(MIP1_SIZE * MIP1_SIZE, Color.red), 1);

            // Upload WITHOUT regenerating, so the hand-authored red mip 1 is what gets serialized.
            source.Apply(updateMipmaps: false);

            using HashKey key = HashKey.FromString("mip-entry");

            EnumResult<TaskError> put = await typedCache.PutAsync(key, EXTENSION, new TextureData(AnyTexture.FromTexture2D(source)), CancellationToken.None);
            Assert.That(put.Success, Is.True);

            EnumResult<Option<TextureData>, TaskError> read = await typedCache.ContentAsync(key, EXTENSION, CancellationToken.None);
            Assert.That(read.Success, Is.True);
            Assert.That(read.Value.Has, Is.True);

            using TextureData deserialized = read.Value.Value;
            Texture2D texture = deserialized.EnsureTexture2D();

            Color[] mip1 = texture.GetPixels(1);
            var mismatches = 0;

            foreach (Color c in mip1)
                if (c != Color.red)
                    mismatches++;

            Assert.That(mismatches, Is.Zero,
                "Deserialized mip 1 must equal the serialized red sentinel; the parameterless Apply() would regenerate it from black mip 0.");
        }

        private static Color[] Filled(int count, Color color)
        {
            var arr = new Color[count];

            for (var i = 0; i < count; i++)
                arr[i] = color;

            return arr;
        }
    }
}
