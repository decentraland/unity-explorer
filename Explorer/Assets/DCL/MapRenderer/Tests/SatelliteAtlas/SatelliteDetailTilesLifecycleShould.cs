using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    /// <summary>
    ///     Drives a <see cref="SatelliteDetailTiles" /> through its load lifecycle with a substituted web request controller, disk cache
    ///     and culling controller. The manifest publishes section (0, 0) only, so the camera over it shows the level-3 tile (0, 0) and
    ///     the level-4 tile (0, 0), and a camera over any other section shows nothing.
    /// </summary>
    public class SatelliteDetailTilesLifecycleShould
    {
        private const string BASE_URL = "http://tiles.test";
        private const string CACHE_EXTENSION = "ktx2";
        private const float BUNDLED_CHUNK_SIZE = 800f;
        private const int RENDER_TEXTURE_SIZE = 1024;
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        /// <summary>Longer than the settle wait, so a refresh that requests nothing has had its chance.</summary>
        private static readonly TimeSpan GRACE = TimeSpan.FromMilliseconds(500);

        private static readonly string LEVEL_3_TILE_URL = TileUrl(0, 0, SatelliteDetailTiles.BASE_LEVEL);
        private static readonly string LEVEL_4_TILE_URL = TileUrl(0, 0, SatelliteDetailTiles.BASE_LEVEL + 1);
        private static readonly string FIXTURE_PATH = Path.Combine(Application.dataPath, "DCL/MapRenderer/Tests/SatelliteAtlas/SatelliteTileFixture.bytes");

        // The render texture over this rect gives level 4; the rect lies inside level-4 tile (0, 0), x 0..400 and y -400..0.
        private static readonly Rect IN_VIEW = new (10, -390, 380, 380);

        // Over section (2, 0), which the manifest doesn't publish.
        private static readonly Rect OUT_OF_VIEW = new (1610, -390, 380, 380);

        private readonly List<string> downloads = new ();
        private readonly Dictionary<string, Func<UniTask<byte[]>>> responses = new ();
        private readonly Dictionary<string, Texture2D> decodedTextures = new ();
        private readonly List<CameraState> cameraStates = new ();

        private byte[] ktx2Tile = null!;
        private IWebRequestController webRequestController = null!;
        private IDiskCache<byte[]> diskCache = null!;
        private IMapCullingController cullingController = null!;
        private GameObject root = null!;
        private GameObject cameraObject = null!;
        private RenderTexture renderTexture = null!;
        private SatelliteDetailTiles tiles = null!;
        private float clockOffset;
        private int removedCachedFiles;

        [SetUp]
        public void SetUp()
        {
            // A failed tile is reported through ReportHub as an exception log.
            LogAssert.ignoreFailingMessages = true;
            ktx2Tile = File.ReadAllBytes(FIXTURE_PATH);
            clockOffset = 0f;
            removedCachedFiles = 0;

            webRequestController = Substitute.For<IWebRequestController>();

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>, SatelliteManifest.Dto>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>>())
               .Returns(UniTask.FromResult(new SatelliteManifest.Dto { sections = new Dictionary<string, string?> { ["0,0"] = "v1" } }));

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>, byte[]>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.GetDataCopyOp<GenericGetRequest>>())
               .Returns(call =>
                {
                    string url = call.Arg<RequestEnvelope<GenericGetRequest, GenericGetArguments>>().CommonArguments.URL;
                    downloads.Add(url);
                    return responses.TryGetValue(url, out Func<UniTask<byte[]>>? response) ? response() : UniTask.FromResult(ktx2Tile);
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

            cullingController = Substitute.For<IMapCullingController>();
            cullingController.CameraStates.Returns(cameraStates);

            cameraObject = new GameObject("map camera");
            renderTexture = new RenderTexture(RENDER_TEXTURE_SIZE, RENDER_TEXTURE_SIZE, 0);
            IMapCameraControllerInternal mapCamera = Substitute.For<IMapCameraControllerInternal>();
            mapCamera.Camera.Returns(cameraObject.AddComponent<Camera>());
            mapCamera.GetRenderTexture().Returns(renderTexture);
            cameraStates.Add(new CameraState { CameraController = mapCamera, Rect = IN_VIEW });

            root = new GameObject("satellite atlas");
            var templateObject = new GameObject("template");
            templateObject.transform.SetParent(root.transform);
            var template = templateObject.AddComponent<SpriteRenderer>();
            var loadingObject = new GameObject("loading");
            loadingObject.transform.SetParent(templateObject.transform);
            AtlasChunk chunk = templateObject.AddComponent<AtlasChunk>();
            typeof(AtlasChunk).GetProperty(nameof(AtlasChunk.MainSpriteRenderer))!.SetValue(chunk, template);
            typeof(AtlasChunk).GetProperty(nameof(AtlasChunk.LoadingSpriteRenderer))!.SetValue(chunk, loadingObject.AddComponent<SpriteRenderer>());

            tiles = new SatelliteDetailTiles(BASE_URL, webRequestController, diskCache, cullingController, template, drawOrderOfBaseLevel: 0);
            tiles.clockOverride = () => UnityEngine.Time.realtimeSinceStartup + clockOffset;
        }

        [TearDown]
        public void TearDown()
        {
            tiles.Dispose();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(renderTexture);

            foreach (Texture2D texture in decodedTextures.Values)
                Object.DestroyImmediate(texture);

            downloads.Clear();
            responses.Clear();
            decodedTextures.Clear();
            cameraStates.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public async Task ShowATileReadFromTheDiskCacheWithoutDownloadingIt()
        {
            // Arrange
            RequireNativeDecoder(requireSampling: true);
            SetDiskCacheContent(Option<byte[]>.Some(ktx2Tile));

            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_4_TILE_URL), "the level-4 tile to show from the disk cache");

            // Assert
            Assert.IsEmpty(downloads);
        }

        [Test]
        public async Task DownloadAndStoreATileMissingFromTheDiskCache()
        {
            // Arrange
            tiles.decodeOverride = (_, url) => UniTask.FromResult(NewDecodedTexture(url));

            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 1, "the level-4 tile to download");

            // Assert
            await diskCache.Received(1).PutAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, ktx2Tile, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task NeverRequestAgainATileTheServerDoesNotHave()
        {
            // Arrange
            UnityWebRequestException notFound = await NotFoundExceptionAsync();
            responses[LEVEL_4_TILE_URL] = () => UniTask.FromException<byte[]>(notFound);

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 1, "the level-4 tile to be requested");

            // Act: the tile leaves the view and comes back
            MoveCamera(OUT_OF_VIEW);
            await Task.Delay(GRACE);
            MoveCamera(IN_VIEW);
            await Task.Delay(GRACE);

            // Assert
            Assert.AreEqual(1, DownloadCount(LEVEL_4_TILE_URL));
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task RetryAFailedTileOnlyAfterTheWaitEvenIfItLeftTheView()
        {
            // Arrange
            responses[LEVEL_4_TILE_URL] = () => UniTask.FromException<byte[]>(new Exception("offline"));

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 1, "the level-4 tile to be requested");

            // Act: the tile leaves the view and comes back before the wait is over
            MoveCamera(OUT_OF_VIEW);
            await Task.Delay(GRACE);
            MoveCamera(IN_VIEW);
            await Task.Delay(GRACE);

            // Assert
            Assert.AreEqual(1, DownloadCount(LEVEL_4_TILE_URL), "A failed tile is not requested again before its wait is over");

            // Act: the wait is over
            clockOffset = SatelliteDetailTiles.RETRY_FAILED_TILE_AFTER_SECONDS + 1f;
            MoveCamera(IN_VIEW);

            // Assert
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 2, "the failed tile to be requested again");
        }

        [Test]
        public async Task RemoveACachedFileThatFailsToDecode()
        {
            // Arrange
            RequireNativeDecoder(requireSampling: false);
            SetDiskCacheContent(Option<byte[]>.Some(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => removedCachedFiles > 0, "the corrupt file to be removed from the disk cache");
            await Task.Delay(GRACE);

            // Assert: a failed decode waits like any failure, it doesn't download right away
            Assert.IsEmpty(downloads);
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task DestroyTheTextureOfATileDroppedWhileDecoding()
        {
            // Arrange
            var decoding = new UniTaskCompletionSource<Texture2D>();
            var decodingStarted = false;
            var texture = new Texture2D(4, 4);

            tiles.decodeOverride = (_, url) =>
            {
                if (url != LEVEL_4_TILE_URL)
                    return UniTask.FromResult(NewDecodedTexture(url));

                decodingStarted = true;
                return decoding.Task;
            };

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => decodingStarted, "the level-4 tile to start decoding");

            // Act: the tile leaves the view while decoding, then its texture arrives
            MoveCamera(OUT_OF_VIEW);
            decoding.TrySetResult(texture);
            await UntilAsync(() => tiles.ActiveLoads == 0, "the dropped load to finish");

            // Assert
            Assert.IsTrue(texture == null, "The texture decoded for a dropped tile is destroyed");
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task ReleaseEveryTileOutOfViewWhenACameraIsReleased()
        {
            // Arrange
            tiles.decodeOverride = (_, url) => UniTask.FromResult(NewDecodedTexture(url));

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_4_TILE_URL), "the level-4 tile to show");

            MoveCamera(OUT_OF_VIEW);
            await Task.Delay(GRACE);
            Assert.IsFalse(decodedTextures[LEVEL_4_TILE_URL] == null, "A loaded tile out of view stays loaded while the camera is rented");

            // Act
            cameraStates.Clear();
            cullingController.CamerasChanged += Raise.Event<Action>();

            // Assert
            Assert.IsTrue(decodedTextures[LEVEL_4_TILE_URL] == null, "The texture of a tile out of view is destroyed once a camera is released");
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task NotStoreADownloadThatFailsToDecode()
        {
            // Arrange
            tiles.decodeOverride = (_, _) => UniTask.FromException<Texture2D>(new Exception("not a KTX2 file"));

            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 1, "the level-4 tile to download");
            await Task.Delay(GRACE);

            // Assert
            await diskCache.DidNotReceive().PutAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            Assert.AreEqual(0, removedCachedFiles, "A downloaded file was never stored, so there is nothing to remove");
        }

        [Test]
        public async Task ReleaseTheFinerTilesButKeepTheBaseLevelWhenTheLayerIsHidden()
        {
            // Arrange
            tiles.decodeOverride = (_, url) => UniTask.FromResult(NewDecodedTexture(url));

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_3_TILE_URL) && IsShown(LEVEL_4_TILE_URL), "the level-3 and level-4 tiles to show");

            // Act
            tiles.ReleaseFinerTiles();

            // Assert
            Assert.IsTrue(decodedTextures[LEVEL_4_TILE_URL] == null, "The texture of a finer tile is destroyed once the layer is hidden");
            Assert.IsTrue(IsShown(LEVEL_3_TILE_URL));
        }

        [Test]
        public void StartWithEveryBaseTileMissingFromASynchronousDiskCache()
        {
            // Arrange
            var sections = new Dictionary<string, string?>();

            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                sections[$"{x},{y}"] = "v1";

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>, SatelliteManifest.Dto>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>>())
               .Returns(UniTask.FromResult(new SatelliteManifest.Dto { sections = sections }));

            Exception? unobserved = null;
            void Capture(Exception e) => unobserved ??= e;
            UniTaskScheduler.UnobservedTaskException += Capture;

            // Act
            try { tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE); }
            finally { UniTaskScheduler.UnobservedTaskException -= Capture; }

            // Assert
            Assert.IsNull(unobserved, "Loads that complete synchronously must not break the pump");
            Assert.AreEqual(0, tiles.ActiveLoads);
        }

        private static string TileUrl(int i, int j, int level) =>
            $"{BASE_URL}/sections/0%2C0/v1/{level}/{i}%2C{j}.ktx2";

        /// <summary>Skips the test where the KTX2 native decoder, or the GPU format it decodes the fixture to, is missing.</summary>
        private static void RequireNativeDecoder(bool requireSampling)
        {
            if (!KtxNativeSupport.IsSupported)
                Assert.Ignore("The ktx_unity native plugin can't be loaded on this machine");

            if (requireSampling && !SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Sample))
                Assert.Ignore("The graphics device can't sample the fixture's format");
        }

        /// <summary>A genuine 404 from a loopback server, as the web request layer raises for a missing tile.</summary>
        private static async Task<UnityWebRequestException> NotFoundExceptionAsync()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            Task serving = Task.Run(async () =>
            {
                HttpListenerContext context = await listener.GetContextAsync();
                context.Response.StatusCode = 404;
                context.Response.Close();
            });

            try
            {
                using UnityWebRequest request = UnityWebRequest.Get($"http://127.0.0.1:{port}/missing-tile.ktx2");

                try { await request.SendWebRequest().WithCancellation(CancellationToken.None); }
                catch (UnityWebRequestException e) { return e; }

                throw new InvalidOperationException("The loopback server should have answered 404");
            }
            finally
            {
                // Stopping the listener aborts a still-pending GetContextAsync with an exception, drained here.
                listener.Stop();
                listener.Close();

                try { await serving; }
                catch (Exception) { }
            }
        }

        private void SetDiskCacheContent(Option<byte[]> content)
        {
            diskCache.ContentAsync(Arg.Any<HashKey>(), CACHE_EXTENSION, Arg.Any<CancellationToken>())
                     .Returns(UniTask.FromResult(EnumResult<Option<byte[]>, TaskError>.SuccessResult(content)));
        }

        private Texture2D NewDecodedTexture(string url)
        {
            var texture = new Texture2D(4, 4) { name = url };
            decodedTextures[url] = texture;
            return texture;
        }

        private void MoveCamera(Rect rect)
        {
            cameraStates[0].Rect = rect;
            cullingController.CamerasChanged += Raise.Event<Action>();
        }

        private int DownloadCount(string url)
        {
            var count = 0;

            for (var i = 0; i < downloads.Count; i++)
                if (downloads[i] == url)
                    count++;

            return count;
        }

        /// <summary>Whether an active view draws a texture decoded from <paramref name="url" />: the decoder names the texture after it.</summary>
        private bool IsShown(string url)
        {
            foreach (AtlasChunk view in root.GetComponentsInChildren<AtlasChunk>())
            {
                Sprite? sprite = view.MainSpriteRenderer.sprite;

                if (view.gameObject.activeSelf && sprite != null && sprite.texture != null && sprite.texture.name.StartsWith(url, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static async Task UntilAsync(Func<bool> condition, string what)
        {
            DateTime deadline = DateTime.UtcNow + TIMEOUT;

            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"Timed out waiting for {what}");

                await Task.Delay(20);
            }
        }
    }
}
