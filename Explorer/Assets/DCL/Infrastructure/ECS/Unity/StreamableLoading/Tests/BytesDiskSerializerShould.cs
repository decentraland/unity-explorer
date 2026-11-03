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

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class BytesDiskSerializerShould
    {
        private const string EXTENSION = "bin";

        private MockedReportScope mockedReportScope = null!;
        private string directoryPath = null!;
        private DiskCache<byte[], SerializeMemoryIterator<BytesDiskSerializer.State>> cache = null!;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();
            var directory = CacheDirectory.New($"TestBytesDiskCache-{Guid.NewGuid():N}");
            directoryPath = directory.Path;
            cache = new DiskCache<byte[], SerializeMemoryIterator<BytesDiskSerializer.State>>(new DiskCache(directory, new FilesLock(), IDiskCleanUp.None.INSTANCE), new BytesDiskSerializer());
        }

        [TearDown]
        public void TearDown()
        {
            mockedReportScope.Dispose();

            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
        }

        [Test]
        public async Task RoundTripBytesSpanningSeveralChunks()
        {
            // Arrange: three full chunks and a partial one, so every branch of the iterator runs
            var bytes = new byte[(SerializeMemoryIterator.CHUNK_SIZE * 3) + 17];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(i * 31);

            using HashKey key = HashKey.FromString("tile");

            // Act
            EnumResult<TaskError> putResult = await cache.PutAsync(key, EXTENSION, bytes, CancellationToken.None);
            EnumResult<Option<byte[]>, TaskError> content = await cache.ContentAsync(key, EXTENSION, CancellationToken.None);

            // Assert
            Assert.IsTrue(putResult.Success);
            Assert.IsTrue(content.Success);
            Assert.IsTrue(content.Value.Has);
            CollectionAssert.AreEqual(bytes, content.Value.Value);
        }

        [Test]
        public async Task ReturnNoneForAMissingEntry()
        {
            // Arrange
            using HashKey key = HashKey.FromString("missing");

            // Act
            EnumResult<Option<byte[]>, TaskError> content = await cache.ContentAsync(key, EXTENSION, CancellationToken.None);

            // Assert
            Assert.IsTrue(content.Success);
            Assert.IsFalse(content.Value.Has);
        }
    }
}
