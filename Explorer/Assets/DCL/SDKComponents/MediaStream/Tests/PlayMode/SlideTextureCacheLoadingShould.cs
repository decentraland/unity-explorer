#if UNITY_EDITOR
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class SlideTextureCacheLoadingShould
    {
        private const string BOT = "presentation-bot:room:1";
        private const string SLIDE_PATH = "/presentations/0f8fad5b-d9cb-469f-a165-70867728950e/slides/0123456789abcdef.png";
        private const double TIMEOUT_SECONDS = 10;

        private HttpListener listener = null!;
        private SlideTextureCache cache = null!;
        private IDisposable lease = null!;
        private string slideUrl = null!;
        private byte[] body = Array.Empty<byte>();

        [SetUp]
        public void SetUp()
        {
            int port = FreeLoopbackPort();
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            _ = ServeAsync(listener);
            slideUrl = $"http://127.0.0.1:{port}{SLIDE_PATH}";

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.CastPresenterService).Returns("https://cast-presenter-service.decentraland.org");
            cache = new SlideTextureCache(TestWebRequestController.INSTANCE, urlsSource);
            lease = cache.BeginComposing();
        }

        [TearDown]
        public void TearDown()
        {
            lease.Dispose();
            cache.Dispose();
            listener.Stop();
            listener.Close();
        }

        [Test]
        public async Task LoadNonReadableClampedSlide_WhenServedPngIsWithinLimits()
        {
            // Arrange
            body = Png(4, 2);

            // Act
            EnumResult<Texture2D, SlideState> result = await RequestUntilSettledAsync();

            // Assert
            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(4, result.Value.width);
            Assert.AreEqual(2, result.Value.height);
            Assert.IsFalse(result.Value.isReadable);
            Assert.AreEqual(TextureWrapMode.Clamp, result.Value.wrapMode);
        }

        [Test]
        public async Task RejectSlide_WhenServedPngIsLargerThanMaxSize()
        {
            // Arrange
            body = Png(PresentationBotMetadata.MAX_SLIDE_SIZE + 1, 1);
            LogAssert.Expect(LogType.Warning, new Regex("Slide rejected"));

            // Act
            EnumResult<Texture2D, SlideState> result = await RequestUntilSettledAsync();

            // Assert
            Assert.AreEqual(SlideState.Rejected, result.Error?.State);
        }

        private async Task<EnumResult<Texture2D, SlideState>> RequestUntilSettledAsync()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(TIMEOUT_SECONDS);
            EnumResult<Texture2D, SlideState> result = cache.GetOrRequest(slideUrl, BOT);

            while (result.Error?.State == SlideState.Loading && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
                result = cache.GetOrRequest(slideUrl, BOT);
            }

            return result;
        }

        private async Task ServeAsync(HttpListener server)
        {
            while (server.IsListening)
            {
                HttpListenerContext context;

                try { context = await server.GetContextAsync().ConfigureAwait(false); }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }

                context.Response.ContentType = "image/png";
                await context.Response.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        private static byte[] Png(int width, int height)
        {
            var source = new Texture2D(width, height);
            byte[] png = source.EncodeToPNG();
            UnityObjectUtils.SafeDestroy(source);
            return png;
        }

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return freePort;
        }
    }
}
#endif
