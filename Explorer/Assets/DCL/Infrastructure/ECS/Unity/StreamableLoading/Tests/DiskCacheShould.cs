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

namespace ECS.StreamableLoading.Tests
{
    /// <summary>
    ///     Covers the regression where a disk-cache write interrupted mid-stream (e.g. by the cancellation
    ///     of a short-lived consumer) left a truncated file at the final path, which was then served as a
    ///     valid cache entry on every subsequent read and broke deserialization forever.
    /// </summary>
    [TestFixture]
    public class DiskCacheShould
    {
        private const string EXTENSION = "tst";
        private const int CHUNK_SIZE = 8;

        private MockedReportScope mockedReportScope = null!;
        private string directoryPath = null!;
        private DiskCache diskCache = null!;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();
            var directory = CacheDirectory.New($"TestDiskCache-{Guid.NewGuid():N}");
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

        [Test]
        public async Task RoundTripEntry()
        {
            // Arrange
            using HashKey key = HashKey.FromString("entry");
            var typedCache = new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(diskCache, new StringDiskSerializer());

            // Act
            EnumResult<TaskError> putResult = await typedCache.PutAsync(key, EXTENSION, "cached value", CancellationToken.None);
            EnumResult<Option<string>, TaskError> readResult = await typedCache.ContentAsync(key, EXTENSION, CancellationToken.None);

            // Assert
            Assert.That(putResult.Success, Is.True);
            Assert.That(readResult.Success, Is.True);
            Assert.That(readResult.Value.Has, Is.True);
            Assert.That(readResult.Value.Value, Is.EqualTo("cached value"));
        }

        [Test]
        public async Task NotLeaveTruncatedFileWhenWriteIsCancelledMidStream()
        {
            // Arrange
            using HashKey key = HashKey.FromString("entry");
            using var iterator = new ChunkedIterator(0x41, totalChunks: 8, throwOnChunk: 4);

            // Act
            EnumResult<TaskError> putResult = await diskCache.PutAsync(key, EXTENSION, iterator, CancellationToken.None);

            // Assert — neither a truncated final file nor an orphaned temp file may remain
            Assert.That(putResult.Success, Is.False);
            Assert.That(Directory.GetFiles(directoryPath), Is.Empty,
                "An interrupted write must not leave any file behind: a truncated entry would be served "
                + "as valid cache content on every subsequent read");
        }

        [Test]
        public async Task KeepPreviousEntryWhenOverwriteIsInterrupted()
        {
            // Arrange — a complete entry already on disk
            using HashKey key = HashKey.FromString("entry");
            using var originalContent = new ChunkedIterator(0x41, totalChunks: 4, throwOnChunk: -1);
            EnumResult<TaskError> firstPut = await diskCache.PutAsync(key, EXTENSION, originalContent, CancellationToken.None);
            Assert.That(firstPut.Success, Is.True);

            // Act — an overwrite of the same entry is interrupted mid-stream
            using var interruptedContent = new ChunkedIterator(0x42, totalChunks: 4, throwOnChunk: 2);
            EnumResult<TaskError> secondPut = await diskCache.PutAsync(key, EXTENSION, interruptedContent, CancellationToken.None);
            Assert.That(secondPut.Success, Is.False);

            // Assert — the previous complete entry is still intact
            EnumResult<SlicedOwnedMemory<byte>?, TaskError> readResult = await diskCache.ContentAsync(key, EXTENSION, CancellationToken.None);
            Assert.That(readResult.Success, Is.True);
            Assert.That(readResult.Value, Is.Not.Null);

            using SlicedOwnedMemory<byte> data = readResult.Value!.Value;
            Assert.That(data.Memory.Length, Is.EqualTo(4 * CHUNK_SIZE));

            for (var i = 0; i < data.Memory.Length; i++)
                Assert.That(data.Memory.Span[i], Is.EqualTo(0x41), $"Byte {i} should belong to the original entry");
        }

        [Test]
        public async Task DropCorruptEntryAndReportMissOnFailedDeserialization()
        {
            // Arrange — a stored entry whose deserialization fails (stands in for a truncated/corrupt file)
            using HashKey key = HashKey.FromString("entry");
            var typedCache = new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(diskCache, new ThrowingDeserializer());

            EnumResult<TaskError> putResult = await typedCache.PutAsync(key, EXTENSION, "cached value", CancellationToken.None);
            Assert.That(putResult.Success, Is.True);

            // Act
            EnumResult<Option<string>, TaskError> readResult = await typedCache.ContentAsync(key, EXTENSION, CancellationToken.None);

            // Assert — the corrupt entry is reported as an error (treated as a miss upstream) and removed,
            // so the asset can be re-downloaded and re-cached instead of failing on the same entry forever
            Assert.That(readResult.Success, Is.False);

            EnumResult<SlicedOwnedMemory<byte>?, TaskError> rawResult = await diskCache.ContentAsync(key, EXTENSION, CancellationToken.None);
            Assert.That(rawResult.Success, Is.True);
            Assert.That(rawResult.Value, Is.Null, "The corrupt entry should have been removed from disk");
        }

        /// <summary>
        ///     Streams a fixed number of equal chunks and optionally throws <see cref="OperationCanceledException" />
        ///     when a given chunk is requested, simulating a write cancelled mid-stream.
        /// </summary>
        private class ChunkedIterator : IMemoryIterator
        {
            private readonly byte[] chunk;
            private readonly int totalChunks;
            private readonly int throwOnChunk;
            private int index = -1;

            public ChunkedIterator(byte fillByte, int totalChunks, int throwOnChunk)
            {
                chunk = new byte[CHUNK_SIZE];

                for (var i = 0; i < chunk.Length; i++)
                    chunk[i] = fillByte;

                this.totalChunks = totalChunks;
                this.throwOnChunk = throwOnChunk;
            }

            public ReadOnlyMemory<byte> Current => chunk;

            public int? TotalSize => totalChunks * CHUNK_SIZE;

            public bool MoveNext()
            {
                index++;

                if (index == throwOnChunk)
                    throw new OperationCanceledException();

                return index < totalChunks;
            }

            public void Dispose() { }
        }

        private class ThrowingDeserializer : IDiskSerializer<string, SerializeMemoryIterator<StringDiskSerializer.State>>
        {
            private readonly StringDiskSerializer serializer = new ();

            public SerializeMemoryIterator<StringDiskSerializer.State> Serialize(string data) =>
                serializer.Serialize(data);

            public UniTask<string> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
            {
                data.Dispose();
                throw new InvalidDataException("Corrupt entry");
            }
        }
    }
}
