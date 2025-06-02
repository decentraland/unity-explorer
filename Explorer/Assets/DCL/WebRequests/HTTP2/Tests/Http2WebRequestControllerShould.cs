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
    public class Http2WebRequestControllerShould
    {
        private const string NOT_FOUND_URL = "https://ab-cdn.decentraland.org/LOD/1/not_found_1_windows";

        private IWebRequestController webRequestController;

        [SetUp]
        public void SetUp()
        {
            ArtificialDelayWebRequestController.IReadOnlyOptions? delay = Substitute.For<ArtificialDelayWebRequestController.IReadOnlyOptions>();
            delay.GetOptionsAsync().Returns(UniTask.FromResult<(float ArtificialDelaySeconds, bool UseDelay)>((0, false)));

            IWebRequestController tw = TestWebRequestController.Create(WebRequestsMode.HTTP2);

            webRequestController = new RedirectWebRequestController(WebRequestsMode.HTTP2,
                                       Substitute.For<IWebRequestController>(),
                                       tw,
                                       Substitute.For<IWebRequestController>(), tw.requestHub)
                                  .WithLog()
                                  .WithArtificialDelay(delay)
                                  .WithBudget(10, new ElementBinding<ulong>(0));
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
