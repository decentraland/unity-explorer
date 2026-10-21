using Cysharp.Threading.Tasks;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    /// <summary>Uses the real KTX2 decoder.</summary>
    public class SatelliteTileLoaderShould
    {
        private const string TILE_URL = "http://tiles.test/sections/0%2C0/v1/4/0%2C0.ktx2";
        private const string CACHE_EXTENSION = "ktx2";
        private static readonly string FIXTURE_PATH = Path.Combine(Application.dataPath, "DCL/MapRenderer/Tests/SatelliteAtlas/SatelliteTileFixture.bytes");
        private static readonly byte[] CORRUPT_FILE = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private readonly List<string> downloads = new ();

        private byte[] ktx2Tile = null!;
        private byte[] served = null!;
        private IWebRequestController webRequestController = null!;
        private IDiskCache<byte[]> diskCache = null!;
        private SatelliteTileLoader loader = null!;
        private Texture2D? texture;
        private int removedCachedFiles;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The first IsSupported read probes the native plugin with an empty buffer, and the plugin logs an error for it.
            LogAssert.ignoreFailingMessages = true;
            _ = KtxNativeSupport.IsSupported;
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = false;
            ktx2Tile = File.ReadAllBytes(FIXTURE_PATH);
            served = ktx2Tile;
            texture = null;
            removedCachedFiles = 0;

            webRequestController = Substitute.For<IWebRequestController>();

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>, byte[]>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>>())
               .Returns(call =>
                {
                    downloads.Add(call.Arg<RequestEnvelope<GenericGetRequest, GenericGetArguments>>().CommonArguments.URL);
                    return UniTask.FromResult<byte[]?>(served);
                });

            diskCache = Substitute.For<IDiskCache<byte[]>>();
            SetDiskCacheContent(Option<byte[]>.None);

            diskCache.PutAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                     .Returns(UniTask.FromResult(EnumResult<TaskError>.SuccessResult()));

            diskCache.RemoveAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<CancellationToken>())
                     .Returns(_ =>
                      {
                          removedCachedFiles++;
                          return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
                      });

            loader = new SatelliteTileLoader(webRequestController, diskCache);
        }

        [TearDown]
        public void TearDown()
        {
            loader.Dispose();

            if (texture != null)
                Object.DestroyImmediate(texture);

            downloads.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public async Task ReadATileFromTheDiskCacheWithoutDownloadingIt()
        {
            // Arrange
            RequireNativeDecoder(requireSampling: true);
            SetDiskCacheContent(Option<byte[]>.Some(ktx2Tile));

            // Act
            texture = await loader.LoadAsync(TILE_URL, cacheOnly: false, CancellationToken.None);

            // Assert
            Assert.IsNotNull(texture);
            Assert.IsEmpty(downloads);
        }

        [Test]
        public async Task DownloadAndStoreATileMissingFromTheDiskCache()
        {
            // Arrange
            RequireNativeDecoder(requireSampling: true);

            // Act
            texture = await loader.LoadAsync(TILE_URL, cacheOnly: false, CancellationToken.None);

            // Assert
            Assert.IsNotNull(texture);
            CollectionAssert.AreEqual(new[] { TILE_URL }, downloads);
            await diskCache.Received(1).PutAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, ktx2Tile, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReturnNothingForACacheOnlyTileMissingFromTheDiskCache()
        {
            // Act
            texture = await loader.LoadAsync(TILE_URL, cacheOnly: true, CancellationToken.None);

            // Assert
            Assert.IsNull(texture);
            Assert.IsEmpty(downloads);
        }

        [Test]
        public async Task NotStoreADownloadThatFailsToDecode()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            RequireNativeDecoder(requireSampling: false);
            served = CORRUPT_FILE;

            // Act
            Exception? failure = await LoadFailureAsync();

            // Assert
            Assert.IsNotNull(failure);
            await diskCache.DidNotReceive().PutAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            Assert.AreEqual(0, removedCachedFiles, "A downloaded file was never stored, so there is nothing to remove");
        }

        [Test]
        public async Task RemoveACachedFileThatFailsToDecode()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            RequireNativeDecoder(requireSampling: false);
            SetDiskCacheContent(Option<byte[]>.Some(CORRUPT_FILE));

            // Act
            Exception? failure = await LoadFailureAsync();

            // Assert: the load fails and does not download the tile
            Assert.IsNotNull(failure);
            Assert.AreEqual(1, removedCachedFiles);
            Assert.IsEmpty(downloads);
        }

        /// <summary>Skips the test where the KTX2 native decoder, or the GPU format it decodes the fixture to, is missing.</summary>
        private static void RequireNativeDecoder(bool requireSampling)
        {
            if (!KtxNativeSupport.IsSupported)
                Assert.Ignore("The ktx_unity native plugin can't be loaded on this machine");

            if (requireSampling && !SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Sample))
                Assert.Ignore("The graphics device can't sample the fixture's format");
        }

        private async Task<Exception?> LoadFailureAsync()
        {
            try { texture = await loader.LoadAsync(TILE_URL, cacheOnly: false, CancellationToken.None); }
            catch (Exception e) { return e; }

            return null;
        }

        private void SetDiskCacheContent(Option<byte[]> content)
        {
            diskCache.ContentAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<CancellationToken>())
                     .Returns(UniTask.FromResult(EnumResult<Option<byte[]>, TaskError>.SuccessResult(content)));
        }
    }
}
