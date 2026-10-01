using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class SlideTextureCacheShould
    {
        private const string ALLOWED_URL = "https://cast-presenter-service.decentraland.org/presentations/a/slides/h.png";

        private IWebRequestController webRequestController = null!;
        private SlideTextureCache cache = null!;

        [SetUp]
        public void SetUp()
        {
            webRequestController = Substitute.For<IWebRequestController>();
            cache = new SlideTextureCache(webRequestController);
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();
        }

        [TestCase(ALLOWED_URL)]
        [TestCase("https://cast-presenter-service.decentraland.zone/presentations/a/slides/h.png")]
        public void AllowUrl_WhenHostIsCastPresenterOnOrgOrZone(string url)
        {
            Assert.IsTrue(SlideTextureCache.IsAllowedUrl(url));
        }

        [TestCase("http://cast-presenter-service.decentraland.org/presentations/a/slides/h.png")]
        [TestCase("https://cast-presenter-service.decentraland.org.evil.com/presentations/a/slides/h.png")]
        [TestCase("https://evil-cast-presenter-service.decentraland.org/presentations/a/slides/h.png")]
        [TestCase("https://example.com/presentations/a/slides/h.png")]
        [TestCase("not a url")]
        public void RejectUrl_WhenHostOrSchemeIsForeign(string url)
        {
            Assert.IsFalse(SlideTextureCache.IsAllowedUrl(url));
        }

        [Test]
        public void ReturnNullAndNeverFetch_WhenUrlIsDisallowed()
        {
            Texture2D? first = cache.GetOrRequest("https://example.com/x.png");
            Texture2D? second = cache.GetOrRequest("https://example.com/x.png");

            Assert.IsNull(first);
            Assert.IsNull(second);
            SendTextureRequest(webRequestController.DidNotReceive());
        }

        [Test]
        public void RequestOnce_WhileInFlight()
        {
            SendTextureRequest(webRequestController).Returns(new UniTaskCompletionSource<Texture2D?>().Task);

            cache.GetOrRequest(ALLOWED_URL);
            cache.GetOrRequest(ALLOWED_URL);
            Texture2D? texture = cache.GetOrRequest(ALLOWED_URL);

            Assert.IsNull(texture);
            SendTextureRequest(webRequestController.Received(1));
        }

        [Test]
        public void NotRetryImmediately_AfterFailure()
        {
            SendTextureRequest(webRequestController).Returns(UniTask.FromException<Texture2D?>(new InvalidOperationException()));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            cache.GetOrRequest(ALLOWED_URL);
            Texture2D? retried = cache.GetOrRequest(ALLOWED_URL);

            Assert.IsNull(retried);
            SendTextureRequest(webRequestController.Received(1));
        }

        private static UniTask<Texture2D?> SendTextureRequest(IWebRequestController controller) =>
            controller.SendAsync<GetTextureWebRequest, GetTextureArguments, GetTextureWebRequest.CreateTextureOp, Texture2D>(
                Arg.Any<RequestEnvelope<GetTextureWebRequest, GetTextureArguments>>(),
                Arg.Any<GetTextureWebRequest.CreateTextureOp>(),
                Arg.Any<long>(),
                Arg.Any<IProgress<float>?>());
    }
}
