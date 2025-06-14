using Best.HTTP;
using Best.HTTP.Caching;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests.HTTP2.Tests
{
    [TestFixture(600 * 1024)] // 600 KB
    [TestFixture(128 * 1024)] // 128 KB
    public class PartialDownloadDataStreamShould
    {
        private readonly long chunkSize;

        // Content-Size: 1_036_789 (>1MB)
        private static readonly Uri PARTIAL_TEST_URL = new ("https://docs.decentraland.org/images/editor/scene-editor.png");

        private static readonly Hash128 PARTIAL_TEST_URL_HASH = HTTPCache.CalculateHash(HTTPMethods.Get, PARTIAL_TEST_URL);

        private static readonly Uri PARTIAL_NOT_SUPPORTED_URL = new ("https://mvfw.org");

        private static readonly Hash128 PARTIAL_NOT_SUPPORTED_URL_HASH = HTTPCache.CalculateHash(HTTPMethods.Get, PARTIAL_NOT_SUPPORTED_URL);

        private static readonly Uri NO_SIZE_HEADERS_URL = new ("https://bunny.net");

        //size 64800
        private static readonly Uri LOCAL_ASSET_PATH = new ($"{Application.dataPath + "/../TestResources/AssetBundles/shark"}");

        private HTTPCache cache;
        private IWebRequestController webRequestController;

        private Http2PartialDownloadDataStream? stream;

        public PartialDownloadDataStreamShould(long chunkSize)
        {
            this.chunkSize = chunkSize;
        }

        [SetUp]
        public void SetUp()
        {
            cache = TestWebRequestController.InitializeCache();

            var requestsHub = new RequestHub(Substitute.For<IDecentralandUrlsSource>(), cache, true, chunkSize, false, WebRequestsMode.YET_ANOTHER);

            webRequestController = new DisposeRequestWrap(new YetAnotherWebRequestController(Substitute.For<IWebRequestsAnalyticsContainer>(),
                Substitute.For<IWeb3IdentityCache>(), requestsHub));
        }

        [TearDown]
        public void RestoreCache()
        {
            TestWebRequestController.RestoreCache();
        }

        [TearDown]
        public void Dispose()
        {
            stream?.Dispose();
            stream = null;
        }

        /// <summary>
        ///     Successfully initializes from cache if partial data was previously cached
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task RecoverPartialDataFromCacheAsync([Values(true, false)] bool finalize)
        {
            ulong contentSize = await GetContentSizeAsync(PARTIAL_TEST_URL);

            var iterationsCount = (int)Math.Ceiling(contentSize / (double)chunkSize);

            // Delete from cache to ensure a fresh start
            cache.Delete(PARTIAL_TEST_URL_HASH, null);

            // Perform one iteration
            stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(PARTIAL_TEST_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                               .GetStreamAsync(CancellationToken.None);
            iterationsCount--;

            if (iterationsCount == 1)
                finalize = true;

            Assert.That(stream, Is.Not.Null);
            Assert.That(stream.IsFullyDownloaded, Is.False);
            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));

            Http2PartialDownloadDataStream.CachedPartialData partialData = stream.GetCachedPartialData();
            long firstItSize = partialData.partialContentLength;
            Assert.That(firstItSize, Is.GreaterThan(0));
            Assert.That(stream.opMode, Is.EqualTo(Http2PartialDownloadDataStream.Mode.WRITING_TO_DISK_CACHE));

            // Close the stream
            stream.Dispose();
            stream = null;

            // At this point the data should be partially cached

            if (!finalize)
                iterationsCount = 1;

            // Perform remaining iterations to finish its downloading

            for (var i = 0; i < iterationsCount; i++)
                stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(PARTIAL_TEST_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                                   .GetStreamAsync(CancellationToken.None);

            Assert.That(stream, Is.Not.Null);
            Assert.That(stream.IsFullyDownloaded, Is.EqualTo(finalize));
            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));
            Assert.That(stream.opMode, Is.EqualTo(finalize ? Http2PartialDownloadDataStream.Mode.COMPLETE_DATA_CACHED : Http2PartialDownloadDataStream.Mode.WRITING_TO_DISK_CACHE));

            partialData = stream.GetCachedPartialData();

            Assert.That(partialData.partialContentLength, Is.GreaterThan(firstItSize));

            if (finalize)
            {
                // Read Handler should be open immediately after the last chunk
                Assert.That(partialData.readHandler, Is.Not.Null);
                Assert.That(partialData.writeHandler, Is.Null);
            }
        }

        [Test]
        public async Task RecoverFullDataFromCacheAsync()
        {
            // Delete from cache to ensure a fresh start
            cache.Delete(PARTIAL_TEST_URL_HASH, null);

            await ConstructDataFromChunksAsync();

            // Close the stream
            stream.Dispose();
            stream = null;

            // At this point the data should be fully cached
            stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(PARTIAL_TEST_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                               .GetStreamAsync(CancellationToken.None);
            Assert.That(stream, Is.Not.Null);
            Assert.That(stream.IsFullyDownloaded, Is.True);
            Assert.That(stream.underlyingStream, Is.Not.Null);
            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));
            Http2PartialDownloadDataStream.CachedPartialData partialData = stream.GetCachedPartialData();
            Assert.That(partialData.partialContentLength, Is.GreaterThan(0));
            Assert.That(stream.opMode, Is.EqualTo(Http2PartialDownloadDataStream.Mode.COMPLETE_DATA_CACHED));
        }

        [Test]
        public async Task ConstructDataFromChunksAsync()
        {
            // Delete from cache to ensure a fresh start
            cache.Delete(PARTIAL_TEST_URL_HASH, null);

            ulong fileSize = await GetContentSizeAsync(PARTIAL_TEST_URL);

            var iterationsCount = (int)Math.Ceiling(fileSize / (double)chunkSize);

            for (var i = 0; i < iterationsCount; i++)
                stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(PARTIAL_TEST_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                                   .GetStreamAsync(CancellationToken.None);

            await UniTask.SwitchToMainThread();

            Assert.That(stream.fullFileSize, Is.EqualTo(fileSize));

            Assert.IsTrue(stream is { IsFullyDownloaded: true });
            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));

            Assert.That(stream.underlyingStream, Is.Not.Null);

            Http2PartialDownloadDataStream.CachedPartialData partialData = stream.GetCachedPartialData();

            Assert.That(partialData.partialContentLength, Is.EqualTo(fileSize));

            // Read Handler should be open immediately after the last chunk
            Assert.That(partialData.readHandler, Is.Not.Null);
            Assert.That(partialData.writeHandler, Is.Null);

            Assert.That(stream.Length, Is.EqualTo(fileSize));
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.True);

            // Compare the stream itself
            byte[]? reliableData = (await UnityWebRequest.Get(PARTIAL_TEST_URL).SendWebRequest()).downloadHandler.data;
            var streamData = new byte[fileSize];

            stream.Read(streamData, 0, streamData.Length);

            Assert.That(streamData.SequenceEqual(reliableData), Is.True);
        }

        //[Test]
        // I could not find a stable endpoint that outputs "Content-Length" but does not support "Range" header
        public async Task ConstructDataFromUnsupportedRangeAsync()
        {
            // Delete from cache to ensure a fresh start
            cache.Delete(PARTIAL_NOT_SUPPORTED_URL_HASH, null);

            ulong fileSize = await GetContentSizeAsync(PARTIAL_NOT_SUPPORTED_URL);

            // The data must be complete for one iteration
            stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(PARTIAL_NOT_SUPPORTED_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                               .GetStreamAsync(CancellationToken.None);

            Assert.IsTrue(stream is { IsFullyDownloaded: true });

            Assert.That(stream.opMode, Is.EqualTo(Http2PartialDownloadDataStream.Mode.COMPLETE_DATA_CACHED));

            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));

            Http2PartialDownloadDataStream.CachedPartialData partialData = stream.GetCachedPartialData();

            Assert.That(partialData.partialContentLength, Is.EqualTo(fileSize));

            // Read Handler should be open immediately after the last chunk
            Assert.That(partialData.readHandler, Is.Not.Null);
            Assert.That(partialData.writeHandler, Is.Null);

            Assert.That(stream.Length, Is.EqualTo(fileSize));
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.True);
        }

        [Test]
        public async Task ConstructUnknownDataInMemoryAsync()
        {
            stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(NO_SIZE_HEADERS_URL, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                               .GetStreamAsync(CancellationToken.None);

            Assert.IsTrue(stream is { IsFullyDownloaded: true });
            Assert.That(stream.GetCachedPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.CachedPartialData)));
            Assert.That(stream.GetFileStreamData(), Is.EqualTo(default(Http2PartialDownloadDataStream.FileStreamData)));

            Http2PartialDownloadDataStream.MemoryStreamPartialData partialData = stream.GetMemoryStreamPartialData();

            Assert.That(partialData.stream, Is.Not.Null);
            Assert.That(stream.underlyingStream, Is.Not.Null);

            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.True);
        }

        [Test]
        public async Task ConstructDataFromFileStream()
        {
            // It should be constructed for one iteration
            stream = (Http2PartialDownloadDataStream)await webRequestController.GetPartialAsync(LOCAL_ASSET_PATH, ReportCategory.PARTIAL_LOADING, new PartialDownloadArguments(stream))
                                                                               .GetStreamAsync(CancellationToken.None);

            Assert.IsTrue(stream is { IsFullyDownloaded: true });
            Assert.That(stream.GetCachedPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.CachedPartialData)));
            Assert.That(stream.GetMemoryStreamPartialData(), Is.EqualTo(default(Http2PartialDownloadDataStream.MemoryStreamPartialData)));

            Http2PartialDownloadDataStream.FileStreamData partialData = stream.GetFileStreamData();

            Assert.That(partialData.stream, Is.Not.Null);
            Assert.That(stream.underlyingStream, Is.Not.Null);

            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.True);
        }

        private async Task<ulong> GetContentSizeAsync(Uri url)
        {
            string? header = await webRequestController.GetAsync(url, ReportCategory.GENERIC_WEB_REQUEST)
                                                       .GetResponseHeaderAsync(WebRequestHeaders.CONTENT_LENGTH_HEADER, CancellationToken.None);

            Assert.IsTrue(WebRequestHeaders.TryParseUnsigned(header, out ulong length));
            return length;
        }
    }
}
