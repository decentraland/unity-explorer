using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.WebRequests.HTTP2.Tests
{
    [TestFixture(WebRequestsMode.HTTP2)]
    [TestFixture(WebRequestsMode.YET_ANOTHER)]
    public class Http2WebRequestControllerShould
    {
        private readonly WebRequestsMode mode;

        private static readonly Uri NOT_FOUND_URL = new ("https://ab-cdn.decentraland.org/LOD/1/not_found_1_windows");

        private IWebRequestController webRequestController;

        public Http2WebRequestControllerShould(WebRequestsMode mode)
        {
            this.mode = mode;
        }

        [SetUp]
        public void SetUp()
        {
            ArtificialDelayWebRequestController.IReadOnlyOptions? delay = Substitute.For<ArtificialDelayWebRequestController.IReadOnlyOptions>();
            delay.GetOptionsAsync().Returns(UniTask.FromResult<(float ArtificialDelaySeconds, bool UseDelay)>((0, false)));

            webRequestController = TestWebRequestController.Create(mode);
        }

        [TearDown]
        public void CleanUp()
        {
            TestWebRequestController.RestoreCache();
        }

        [Test]
        public async Task DisposeWrapOnException([Values(true, false)] bool fromMainThread)
        {
            if (fromMainThread)
                await UniTask.SwitchToMainThread();
            else
                await UniTask.SwitchToThreadPool();

            GenericGetRequest wrap = webRequestController.GetAsync(NOT_FOUND_URL, ReportData.UNSPECIFIED, suppressErrors: true);

            await wrap.SendAsync(CancellationToken.None).SuppressAnyExceptionWithFallback(null);

            Assert.IsTrue(wrap.isDisposed);
        }
    }
}
