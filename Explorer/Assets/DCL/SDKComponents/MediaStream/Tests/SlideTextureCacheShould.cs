using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Utility;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class SlideTextureCacheShould
    {
        private const string UUID = "0f8fad5b-d9cb-469f-a165-70867728950e";
        private const string HASH = "0123456789abcdef";
        private const string SLIDE_PATH = "/presentations/" + UUID + "/slides/" + HASH + ".png";
        private const string ORG_ORIGIN = "https://cast-presenter-service.decentraland.org";
        private const string ALLOWED_URL = ORG_ORIGIN + SLIDE_PATH;
        private const string BOT_A = "presentation-bot:a:1";
        private const string BOT_B = "presentation-bot:b:1";

        private IWebRequestController webRequestController = null!;
        private IDecentralandUrlsSource decentralandUrlsSource = null!;
        private SlideTextureCache cache = null!;
        private List<Texture2D> decoded = null!;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            webRequestController = Substitute.For<IWebRequestController>();
            decentralandUrlsSource = Substitute.For<IDecentralandUrlsSource>();
            decentralandUrlsSource.BaseDomain.Returns("decentraland.org");
            cache = new SlideTextureCache(webRequestController, decentralandUrlsSource, () => now);
            decoded = new List<Texture2D>();
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();

            foreach (Texture2D texture in decoded)
                UnityObjectUtils.SafeDestroy(texture);
        }

        [TestCase(ALLOWED_URL)]
        [TestCase(ORG_ORIGIN + "/cast" + SLIDE_PATH)]
        [TestCase("http://localhost:3002" + SLIDE_PATH)]
        public void AllowUrl_WhenCanonicalCastPresenterSlideUrl(string url)
        {
            Assert.IsTrue(cache.IsAllowedUrl(url));
        }

        [Test]
        public void AllowOnlyCastPresenterServiceHost_WhenItIsCustom()
        {
            decentralandUrlsSource.Url(DecentralandUrl.CastPresenterService).Returns("https://cast-presenter-service.example.org");
            var customCache = new SlideTextureCache(webRequestController, decentralandUrlsSource, () => now);

            bool customAllowed = customCache.IsAllowedUrl("https://cast-presenter-service.example.org" + SLIDE_PATH);
            bool orgAllowed = customCache.IsAllowedUrl(ALLOWED_URL);
            customCache.Dispose();

            Assert.IsTrue(customAllowed);
            Assert.IsFalse(orgAllowed);
        }

        [TestCase("http://cast-presenter-service.decentraland.org" + SLIDE_PATH)]
        [TestCase("https://cast-presenter-service.decentraland.zone" + SLIDE_PATH)]
        [TestCase("https://cast-presenter-service.decentraland.org.evil.com" + SLIDE_PATH)]
        [TestCase("https://evil-cast-presenter-service.decentraland.org" + SLIDE_PATH)]
        [TestCase("https://example.com" + SLIDE_PATH)]
        [TestCase("not a url")]
        [TestCase(ORG_ORIGIN + ":8443" + SLIDE_PATH)]
        [TestCase("https://a@cast-presenter-service.decentraland.org" + SLIDE_PATH)]
        [TestCase(ALLOWED_URL + "?x=1")]
        [TestCase(ALLOWED_URL + "#f")]
        [TestCase("http://evil.example\\@localhost" + SLIDE_PATH)]
        [TestCase("https://cast-presenter-service.decentraland.org\\@example.com" + SLIDE_PATH)]
        [TestCase("https://cast-presenter-servıce.decentraland.org" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + "/redirect?to=x")]
        [TestCase(ORG_ORIGIN + "/presentations/a/slides/h.png")]
        [TestCase(ORG_ORIGIN + "/presentations/" + UUID + "/slides/0123456789ABCDEF.png")]
        [TestCase("https://CAST-PRESENTER-SERVICE.DECENTRALAND.ORG" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + ":443" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + "/a.b" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + "/x%2F" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + "/" + SLIDE_PATH)]
        [TestCase(ORG_ORIGIN + SLIDE_PATH + "/")]
        public void RejectUrl_WhenNotCanonicalCastPresenterSlideUrl(string url)
        {
            Assert.IsFalse(cache.IsAllowedUrl(url));
        }

        [Test]
        public void ReturnNullAndNeverFetch_WhenUrlIsDisallowed()
        {
            Texture2D? first = cache.GetOrRequest("https://example.com/x.png", BOT_A);
            Texture2D? second = cache.GetOrRequest("https://example.com/x.png", BOT_A);

            Assert.IsNull(first);
            Assert.IsNull(second);
            SendTextureRequest(webRequestController.DidNotReceive());
        }

        [Test]
        public void RequestOnce_WhileInFlight()
        {
            SendTextureRequest(webRequestController).Returns(new UniTaskCompletionSource<Texture2D?>().Task);

            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            Texture2D? texture = cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsNull(texture);
            SendTextureRequest(webRequestController.Received(1));
        }

        [Test]
        public void BoundTrackedUrls_WhenManyDistinctUrlsAreDisallowed()
        {
            for (var i = 0; i < 1000; i++)
                cache.GetOrRequest($"https://example.com/{i}.png", BOT_A);

            Assert.LessOrEqual(cache.trackedUrlCount, SlideTextureCache.MAX_TRACKED_URLS);
        }

        [Test]
        public void StartOneFetchPerInterval_WhenUrlsChurn()
        {
            SendTextureRequest(webRequestController).Returns(new UniTaskCompletionSource<Texture2D?>().Task);

            for (var i = 0; i < 10; i++)
                cache.GetOrRequest(SlideUrl(i), BOT_A);

            SendTextureRequest(webRequestController.Received(1));

            now += SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS;
            cache.GetOrRequest(SlideUrl(10), BOT_A);

            SendTextureRequest(webRequestController.Received(2));
        }

        [Test]
        public void NotDelayOtherBot_WhenOneBotChurns()
        {
            SendTextureRequest(webRequestController).Returns(_ => new UniTaskCompletionSource<Texture2D?>().Task);

            cache.GetOrRequest(SlideUrl(0), BOT_A);
            cache.GetOrRequest(SlideUrl(1), BOT_B);
            cache.GetOrRequest(SlideUrl(2), BOT_A);

            SendTextureRequest(webRequestController.Received(2));
        }

        [Test]
        public void ForgetThrottleKeys_WhenMoreThanMaxBotsAreTracked()
        {
            SendTextureRequest(webRequestController).Returns(_ => new UniTaskCompletionSource<Texture2D?>().Task);

            for (var i = 0; i <= SlideTextureCache.MAX_TRACKED_BOTS; i++)
                cache.GetOrRequest(SlideUrl(i), $"presentation-bot:{i}:1");

            cache.GetOrRequest(SlideUrl(SlideTextureCache.MAX_TRACKED_BOTS + 1), "presentation-bot:0:1");

            SendTextureRequest(webRequestController.Received(SlideTextureCache.MAX_TRACKED_BOTS + 2));
        }

        [Test]
        public void ReportFailure_AfterRejectionBudgetIsSpent()
        {
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromException<Texture2D?>(new InvalidOperationException()));

            for (var i = 0; i < SlideTextureCache.MAX_REJECTION_REPORTS; i++)
                LogAssert.Expect(LogType.Warning, new Regex("Slide url rejected"));

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            for (var i = 0; i < 40; i++)
                cache.GetOrRequest($"https://example.com/{i}.png", BOT_A);

            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BoundFailedUrls_WhenManyDistinctUrlsFail()
        {
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromException<Texture2D?>(new InvalidOperationException()));

            for (var i = 0; i < SlideTextureCache.MAX_FAILURE_REPORTS; i++)
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            for (var i = 0; i < 40; i++)
            {
                now += SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS;
                cache.GetOrRequest(SlideUrl(i), BOT_A);
            }

            Assert.LessOrEqual(cache.trackedUrlCount, SlideTextureCache.MAX_TRACKED_URLS);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReportFailureOnce_WhenUrlKeepsFailing()
        {
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromException<Texture2D?>(new InvalidOperationException()));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            now += 61f;
            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            SendTextureRequest(webRequestController.Received(2));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RetryAfterCooldown_WhenClockAdvances()
        {
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromException<Texture2D?>(new InvalidOperationException()));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            now = 9f;
            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            SendTextureRequest(webRequestController.Received(1));

            now = 11f;
            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            SendTextureRequest(webRequestController.Received(2));
        }

        [Test]
        public void RejectDecodedSlide_WhenLargerThanMaxSize()
        {
            var oversized = new Texture2D(PresentationLayout.MAX_SLIDE_SIZE + 1, 1);
            SendTextureRequest(webRequestController).Returns(UniTask.FromResult<Texture2D?>(oversized));
            LogAssert.Expect(LogType.Warning, new Regex($"{PresentationLayout.MAX_SLIDE_SIZE + 1}x1"));

            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            now = SlideTextureCache.MAX_RETRY_COOLDOWN_SECONDS + 1f;
            Texture2D? afterCooldown = cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsNull(afterCooldown);
            Assert.IsTrue(oversized == null);
            SendTextureRequest(webRequestController.Received(1));
        }

        [Test]
        public void KeepRecentlyUsedSlide_WhenCacheEvicts()
        {
            var slides = new Texture2D[SlideTextureCache.CAPACITY + 1];

            for (var i = 0; i < slides.Length; i++)
                slides[i] = new Texture2D(2, 2);

            var loaded = 0;
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromResult<Texture2D?>(slides[loaded++]));

            for (var i = 0; i < SlideTextureCache.CAPACITY; i++)
                LoadSlide(i);

            cache.GetOrRequest(SlideUrl(0), BOT_A);
            LoadSlide(SlideTextureCache.CAPACITY);

            Assert.AreSame(slides[0], cache.GetOrRequest(SlideUrl(0), BOT_A));
            Assert.IsTrue(slides[1] == null);
        }

        [Test]
        public void DropCpuCopy_WhenSlideLoads()
        {
            var texture = new Texture2D(2, 2);
            SendTextureRequest(webRequestController).Returns(UniTask.FromResult<Texture2D?>(texture));

            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            Texture2D? loaded = cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.AreSame(texture, loaded);
            Assert.IsFalse(texture.isReadable);
        }

        [Test]
        public void ForgetOnlyOldestRejectedUrl_WhenRejectedSetOverflows()
        {
            for (var i = 0; i <= SlideTextureCache.MAX_TRACKED_URLS; i++)
                cache.GetOrRequest($"https://example.com/{i}.png", BOT_A);

            Assert.AreEqual(SlideTextureCache.MAX_TRACKED_URLS, cache.trackedUrlCount);
        }

        [Test]
        public void RejectAndNeverFetchAgain_WhenSlideIsRejectedBeforeDecoding()
        {
            SendTextureRequest(webRequestController).Returns(UniTask.FromResult<Texture2D?>(null));
            LogAssert.Expect(LogType.Warning, new Regex("Slide rejected"));

            cache.GetOrRequest(ALLOWED_URL, BOT_A);
            now = SlideTextureCache.MAX_RETRY_COOLDOWN_SECONDS + 1f;
            Texture2D? afterCooldown = cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsNull(afterCooldown);
            SendTextureRequest(webRequestController.Received(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DecodeWithoutCpuCopy_WhenPngIsWithinLimits()
        {
            Texture2D? slide = Decode(Png(4, 2));

            Assert.IsNotNull(slide);
            Assert.AreEqual(4, slide!.width);
            Assert.AreEqual(2, slide.height);
            Assert.IsFalse(slide.isReadable);
        }

        [Test]
        public void RejectBeforeDecoding_WhenPngIsLargerThanMaxSize()
        {
            Assert.IsNull(Decode(Png(PresentationLayout.MAX_SLIDE_SIZE + 1, 1)));
        }

        [Test]
        public void RejectBeforeDecoding_WhenBodyIsKtx2()
        {
            var ktx2 = new byte[64];
            new byte[] { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(ktx2, 0);

            Assert.IsNull(Decode(ktx2));
        }

        [Test]
        public void RejectBeforeDecoding_WhenBodyExceedsMaxBytes()
        {
            byte[] png = Png(2, 2);
            var oversized = new byte[SlideTextureCache.MAX_SLIDE_BYTES + 1];
            png.CopyTo(oversized, 0);

            Assert.IsNull(Decode(oversized));
        }

        [Test]
        public void DestroySlidesAndFetchAgain_WhenUnloaded()
        {
            var slide = new Texture2D(2, 2);
            SendTextureRequest(webRequestController).Returns(UniTask.FromResult<Texture2D?>(slide), UniTask.FromResult<Texture2D?>(new Texture2D(2, 2)));
            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            cache.Unload();
            now += SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS;
            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsTrue(slide == null);
            SendTextureRequest(webRequestController.Received(2));
        }

        [Test]
        public void EvictOldestSlide_WhenClearedThrottled()
        {
            var slides = new[] { new Texture2D(2, 2), new Texture2D(2, 2) };
            var loaded = 0;
            SendTextureRequest(webRequestController).Returns(_ => UniTask.FromResult<Texture2D?>(slides[loaded++]));
            LoadSlide(0);
            LoadSlide(1);

            cache.ClearThrottled(1);

            Assert.IsTrue(slides[0] == null);
            Assert.IsTrue(slides[1] != null);
        }

        [Test]
        public void FetchWithoutRedirects()
        {
            RequestEnvelope<GetTextureWebRequest, GetTextureArguments> envelope = default;

            webRequestController.SendAsync<GetTextureWebRequest, GetTextureArguments, GetTextureWebRequest.CreateTextureOp, Texture2D>(
                                     Arg.Do<RequestEnvelope<GetTextureWebRequest, GetTextureArguments>>(e => envelope = e),
                                     Arg.Any<GetTextureWebRequest.CreateTextureOp>(),
                                     Arg.Any<long>(),
                                     Arg.Any<IProgress<float>?>())
                                .Returns(new UniTaskCompletionSource<Texture2D?>().Task);

            cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsTrue(envelope.args.DisableRedirects);
            Assert.AreEqual(ALLOWED_URL, envelope.CommonArguments.URL.Value);
        }

        [Test]
        public void NotThrow_WhenDisposedTwice()
        {
            cache.Dispose();
            cache.Dispose();
        }

        [Test]
        public void NotFetch_WhenRequestedAfterDispose()
        {
            cache.Dispose();

            Texture2D? texture = cache.GetOrRequest(ALLOWED_URL, BOT_A);

            Assert.IsNull(texture);
            SendTextureRequest(webRequestController.DidNotReceive());
            LogAssert.NoUnexpectedReceived();
        }

        private void LoadSlide(int index)
        {
            now += SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS;
            cache.GetOrRequest(SlideUrl(index), BOT_A);
        }

        private Texture2D? Decode(byte[] data)
        {
            Texture2D? slide = SlideTextureCache.DecodeSlide(data);

            if (slide != null)
                decoded.Add(slide);

            return slide;
        }

        private static byte[] Png(int width, int height)
        {
            var source = new Texture2D(width, height);
            byte[] png = source.EncodeToPNG();
            UnityObjectUtils.SafeDestroy(source);
            return png;
        }

        private static string SlideUrl(int index) =>
            $"{ORG_ORIGIN}/presentations/{index:x8}-d9cb-469f-a165-70867728950e/slides/{HASH}.png";

        private static UniTask<Texture2D?> SendTextureRequest(IWebRequestController controller) =>
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, GetTextureWebRequest.CreateTextureOp, Texture2D>(
                Arg.Any<RequestEnvelope<GetTextureWebRequest, GetTextureArguments>>(),
                Arg.Any<GetTextureWebRequest.CreateTextureOp>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<float>?>());
    }
}
