using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    /// <summary>
    ///     The disk cache holds no tile, so a cache-only load returns nothing. The manifest publishes only section (0, 0).
    ///     A camera over that section shows the level-3 tile (0, 0) and the level-4 tile (0, 0). A camera over any other section shows nothing.
    /// </summary>
    public class SatelliteDetailTilesLifecycleShould
    {
        private const string BASE_URL = "http://tiles.test";
        private const float BUNDLED_CHUNK_SIZE = 800f;
        private const int RENDER_TEXTURE_SIZE = 1024;
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        /// <summary>Longer than the settle wait, so a refresh that requests nothing has had its chance.</summary>
        private static readonly TimeSpan GRACE = TimeSpan.FromMilliseconds(500);

        private static readonly string LEVEL_3_TILE_URL = TileUrl(0, 0, SatelliteTileGrid.BASE_LEVEL);
        private static readonly string LEVEL_4_TILE_URL = TileUrl(0, 0, SatelliteTileGrid.BASE_LEVEL + 1);

        // The render texture over this rect gives level 4; the rect lies inside level-4 tile (0, 0), x 0..400 and y -400..0.
        private static readonly Rect IN_VIEW = new (10, -390, 380, 380);

        // Over section (2, 0), which the manifest doesn't publish.
        private static readonly Rect OUT_OF_VIEW = new (1610, -390, 380, 380);

        private readonly List<string> downloads = new ();
        private readonly Dictionary<string, Func<UniTask<Texture2D?>>> responses = new ();
        private readonly Dictionary<string, Texture2D> loadedTextures = new ();
        private readonly List<string> cacheOnlyLoads = new ();
        private readonly List<CameraState> cameraStates = new ();

        private IWebRequestController webRequestController = null!;
        private ISatelliteTileLoader loader = null!;
        private IMapCullingController cullingController = null!;
        private GameObject root = null!;
        private GameObject cameraObject = null!;
        private RenderTexture renderTexture = null!;
        private SatelliteDetailTiles tiles = null!;
        private float clockOffset;

        [SetUp]
        public void SetUp()
        {
            // A failed tile is reported through ReportHub as an exception log.
            LogAssert.ignoreFailingMessages = true;
            clockOffset = 0f;

            webRequestController = Substitute.For<IWebRequestController>();

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>, SatelliteManifest.Dto>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<SatelliteManifest.Dto, GenericGetRequest>>())
               .Returns(UniTask.FromResult<SatelliteManifest.Dto?>(new SatelliteManifest.Dto { sections = new Dictionary<string, string?> { ["0,0"] = "v1" } }));

            loader = Substitute.For<ISatelliteTileLoader>();

            loader.LoadAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                  .Returns(call =>
                   {
                       var url = call.ArgAt<string>(0);

                       if (call.ArgAt<bool>(1))
                       {
                           cacheOnlyLoads.Add(url);
                           return UniTask.FromResult<Texture2D?>(null);
                       }

                       downloads.Add(url);
                       return responses.TryGetValue(url, out Func<UniTask<Texture2D?>>? response) ? response() : UniTask.FromResult<Texture2D?>(NewTexture(url));
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

            tiles = new SatelliteDetailTiles(BASE_URL, webRequestController, loader, cullingController, new SatelliteTileViews(template, drawOrderOfBaseLevel: 0));
            tiles.clockOverride = () => UnityEngine.Time.realtimeSinceStartup + clockOffset;
        }

        [TearDown]
        public void TearDown()
        {
            tiles.Dispose();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(renderTexture);

            foreach (Texture2D texture in loadedTextures.Values)
                Object.DestroyImmediate(texture);

            downloads.Clear();
            responses.Clear();
            loadedTextures.Clear();
            cacheOnlyLoads.Clear();
            cameraStates.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public async Task ShowTheBaseAndTheSharperLevelTilesInView()
        {
            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_3_TILE_URL) && IsShown(LEVEL_4_TILE_URL), "the level-3 and level-4 tiles to show");

            // Assert
            Assert.AreEqual(1, DownloadCount(LEVEL_3_TILE_URL));
            Assert.AreEqual(1, DownloadCount(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task ReadTheBaseLevelFromTheDiskCacheOnlyBeforeACameraShowsIt()
        {
            // Arrange
            MoveCamera(OUT_OF_VIEW);

            // Act
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => cacheOnlyLoads.Contains(LEVEL_3_TILE_URL), "the level-3 tile to be read from the disk cache");
            await Task.Delay(GRACE);

            // Assert
            Assert.IsEmpty(downloads);
        }

        [Test]
        public async Task NeverRequestAgainATileTheServerDoesNotHave()
        {
            // Arrange
            UnityWebRequestException notFound = await NotFoundExceptionAsync();
            responses[LEVEL_4_TILE_URL] = () => UniTask.FromException<Texture2D?>(notFound);

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
            LogAssert.ignoreFailingMessages = true;
            responses[LEVEL_4_TILE_URL] = () => UniTask.FromException<Texture2D?>(new Exception("offline"));

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
        public async Task DestroyTheTextureOfATileDroppedWhileLoading()
        {
            // Arrange
            var loading = new UniTaskCompletionSource<Texture2D?>();
            var texture = new Texture2D(4, 4);
            loadedTextures[LEVEL_4_TILE_URL] = texture;
            responses[LEVEL_4_TILE_URL] = () => loading.Task;

            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => DownloadCount(LEVEL_4_TILE_URL) == 1, "the level-4 tile to start loading");

            // Act: the tile leaves the view while loading, then its texture arrives
            MoveCamera(OUT_OF_VIEW);
            loading.TrySetResult(texture);
            await UntilAsync(() => tiles.ActiveLoads == 0, "the dropped load to finish");

            // Assert
            Assert.IsTrue(texture == null, "The texture loaded for a dropped tile is destroyed");
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task ReleaseEveryTileOutOfViewWhenACameraIsReleased()
        {
            // Arrange
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_4_TILE_URL), "the level-4 tile to show");

            MoveCamera(OUT_OF_VIEW);
            await Task.Delay(GRACE);
            Assert.IsFalse(loadedTextures[LEVEL_4_TILE_URL] == null, "A loaded tile out of view stays loaded while the camera is rented");

            // Act
            cameraStates.Clear();
            cullingController.CamerasChanged += Raise.Event<Action>();

            // Assert
            Assert.IsTrue(loadedTextures[LEVEL_4_TILE_URL] == null, "The texture of a tile out of view is destroyed once a camera is released");
            Assert.IsFalse(IsShown(LEVEL_4_TILE_URL));
        }

        [Test]
        public async Task ReleaseTheFinerTilesButKeepTheBaseLevelWhenTheLayerIsHidden()
        {
            // Arrange
            tiles.Initialize(Vector2.zero, BUNDLED_CHUNK_SIZE);
            await UntilAsync(() => IsShown(LEVEL_3_TILE_URL) && IsShown(LEVEL_4_TILE_URL), "the level-3 and level-4 tiles to show");

            // Act
            tiles.ReleaseFinerTiles();

            // Assert
            Assert.IsTrue(loadedTextures[LEVEL_4_TILE_URL] == null, "The texture of a finer tile is destroyed once the layer is hidden");
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
               .Returns(UniTask.FromResult<SatelliteManifest.Dto?>(new SatelliteManifest.Dto { sections = sections }));

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

        private Texture2D NewTexture(string url)
        {
            var texture = new Texture2D(4, 4) { name = url };
            loadedTextures[url] = texture;
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

        /// <summary>Whether an active view draws the texture loaded for <paramref name="url" />: the loader names the texture after it.</summary>
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
