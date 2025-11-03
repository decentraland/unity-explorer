using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Diagnostics.Tests;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

namespace DCL.SocialService.PerformanceTests
{
    [TestFixture(DecentralandEnvironment.Org)]
    [TestFixture(DecentralandEnvironment.Zone)]
    public class SocialServicePerformanceTests
    {
        private const int ITERATIONS_COUNT = 30;

        private readonly DecentralandEnvironment env;
        private readonly DecentralandUrlsSource urlsSource;

        private IWebRequestController? webRequestController;
        private CommunitiesDataProvider? communitiesDataProvider;

        private PerformanceTestWebRequestsAnalytics? analytics;

        private MockedReportScope? reportScope;

        // We need an identity for Signed Fetch, assume here it is taken from Player Prefabs as we can't authenticate in Tests
        private readonly IWeb3IdentityCache identityCache = new ProxyIdentityCache(
            new MemoryWeb3IdentityCache(),
            new PlayerPrefsIdentityProvider(
                new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(new Web3AccountFactory()))
        );

        public SocialServicePerformanceTests(DecentralandEnvironment env)
        {
            this.env = env;
            urlsSource = new DecentralandUrlsSource(env, ILaunchMode.PLAY);
        }

        [SetUp]
        public void SetUp()
        {
            webRequestController = new WebRequestController(analytics = new PerformanceTestWebRequestsAnalytics(), identityCache, new RequestHub(urlsSource), ChromeDevtoolProtocolClient.NewForTest());
            communitiesDataProvider = new CommunitiesDataProvider(webRequestController, urlsSource, identityCache);

            reportScope = new MockedReportScope();
        }

        [TearDown]
        public void TearDown() =>
            reportScope?.Dispose();

        [Test]
        [TestCase(25)]
        [TestCase(50)]
        [TestCase(100)]
        [Performance]
        public async Task GetUserCommunitiesAsync_KeepAlive(int pageSize) =>
            await BenchmarkAsync(() => communitiesDataProvider.GetUserCommunitiesAsync("", false, 0, pageSize, CancellationToken.None),
                2, ITERATIONS_COUNT, TimeSpan.FromMilliseconds(100));

        [Test]
        [TestCase(25)]
        [TestCase(50)]
        [TestCase(100)]
        [Performance]
        public async Task GetUserCommunitiesAsync_ExpireKeepAlive(int pageSize) =>
            await BenchmarkAsync(() => communitiesDataProvider.GetUserCommunitiesAsync("", false, 0, pageSize, CancellationToken.None),
                0, 10, TimeSpan.FromSeconds(10));

        private async UniTask BenchmarkAsync(Func<UniTask> createTask, int warmupCount, int iterationsCount, TimeSpan delayBetweenIterations)
        {
            analytics!.WarmingUp = true;

            // Warmup a few times (DNS/TLS/JIT)
            for (int i = 0; i < warmupCount; i++)
                await createTask();

            analytics.WarmingUp = false;

            for (int i = 0; i < iterationsCount; i++)
            {
                await createTask();

                await UniTask.Delay(delayBetweenIterations);
            }
        }
    }
}
