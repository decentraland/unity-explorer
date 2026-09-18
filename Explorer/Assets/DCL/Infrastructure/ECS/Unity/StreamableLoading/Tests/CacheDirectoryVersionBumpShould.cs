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

namespace ECS.StreamableLoading.Tests
{
    /// <summary>
    ///     Covers the regression where a disk-cache entry left behind by an older client — truncated by an
    ///     interrupted write, or otherwise corrupted in a way that does not throw on deserialization — was
    ///     served forever because the read path performed no integrity validation and nothing ever purged
    ///     entries written under a previous <see cref="CacheDirectory" /> version. This is the mechanism
    ///     behind the field-reported vending-machine atlas textures that stayed corrupted across restarts
    ///     and even a full reinstall, since the cache lives under Application.persistentDataPath.
    /// </summary>
    [TestFixture]
    public class CacheDirectoryVersionBumpShould
    {
        private const string EXTENSION = "tst";

        // The folder name CacheDirectory resolved to before the fix bumped its CACHE_VERSION constant.
        // CacheDirectory.New/NewDefault always resolve the "current version" folder from the compiled
        // CACHE_VERSION, independently of whatever subdirectory name the caller asks for: on an unpatched
        // client that constant is still "V3", so a directory literally named "DiskCacheV3" IS the live,
        // protected cache directory; once CACHE_VERSION is bumped, the very same "DiskCacheV3" directory
        // becomes a stale sibling that gets swept away the next time a CacheDirectory is constructed.
        private const string LEGACY_VERSION_FOLDER = "DiskCacheV3";

        private MockedReportScope? mockedReportScope;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();
        }

        [TearDown]
        public void TearDown()
        {
            mockedReportScope.Dispose();

            // Defensive cleanup in case a failed assertion above skipped the in-test cleanup: neither
            // folder name is meaningful once the test session ends.
            foreach (string folder in new[] { LEGACY_VERSION_FOLDER, "DiskCacheV4" })
            {
                string path = Path.Combine(Application.persistentDataPath, folder);

                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
        }

        [Test]
        public async Task EvictTruncatedLegacyEntryWhenCacheDirectoryVersionIsBumped()
        {
            // Arrange — write a valid entry via the real put path into the folder an older (pre-bump)
            // client would have used, then truncate its on-disk file: this stands in for a legacy entry
            // corrupted by an interrupted write.
            CacheDirectory legacyDirectory = CacheDirectory.New(LEGACY_VERSION_FOLDER);
            var legacyDiskCache = new DiskCache(legacyDirectory, new FilesLock(), IDiskCleanUp.None.INSTANCE);
            var legacyCache = new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(legacyDiskCache, new StringDiskSerializer());

            using HashKey key = HashKey.FromString($"legacy-entry-{Guid.NewGuid():N}");

            EnumResult<TaskError> putResult = await legacyCache.PutAsync(key, EXTENSION, "cached value", CancellationToken.None);
            Assert.That(putResult.Success, Is.True);

            string fileName = HashNamings.HashNameFrom(key, EXTENSION);
            string filePath = Path.Combine(legacyDirectory.Path, fileName);
            Assert.That(File.Exists(filePath), Is.True);

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write))
                stream.SetLength(stream.Length / 2);

            // Sanity check — the truncated entry is still silently readable straight out of the legacy
            // folder (deserialization does not throw). This confirms the scenario is the field-reported
            // "served forever" case, not the already-handled throws-on-deserialize corruption case.
            EnumResult<Option<string>, TaskError> legacyReadResult = await legacyCache.ContentAsync(key, EXTENSION, CancellationToken.None);
            Assert.That(legacyReadResult.Success, Is.True);
            Assert.That(legacyReadResult.Value.Has, Is.True);

            CacheDirectory currentDirectory = default;

            try
            {
                // Act — construct the CacheDirectory a booting client uses today. CacheDirectory.New
                // sweeps every sibling folder whose name starts with "DiskCache" other than the current
                // version's folder. On an unpatched client CACHE_VERSION is still "V3", so this resolves
                // to the very same directory the legacy entry lives in (protected: nothing is swept). On
                // a patched client CACHE_VERSION has been bumped, so "DiskCacheV3" is now a stale sibling
                // and is deleted wholesale — exactly what cures every affected user on their next update.
                currentDirectory = CacheDirectory.NewDefault();

                var currentDiskCache = new DiskCache(currentDirectory, new FilesLock(), IDiskCleanUp.None.INSTANCE);
                var currentCache = new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(currentDiskCache, new StringDiskSerializer());

                // Assert — looked up through the CURRENT CacheDirectory, the truncated legacy entry must
                // be gone: reported as a miss, never served as truncated/corrupt content.
                EnumResult<Option<string>, TaskError> readResult = await currentCache.ContentAsync(key, EXTENSION, CancellationToken.None);
                Assert.That(readResult.Success, Is.True);
                Assert.That(readResult.Value.Has, Is.False,
                    "A truncated legacy entry left over from before the CacheDirectory version bump must not "
                    + "be served once the current client's CACHE_VERSION has moved past it");
            }
            finally
            {
                if (Directory.Exists(currentDirectory.Path))
                    Directory.Delete(currentDirectory.Path, true);
            }
        }
    }
}
