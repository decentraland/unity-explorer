using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Global.Dynamic.LaunchModes;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture(DecentralandEnvironment.Org)]
    [TestFixture(DecentralandEnvironment.Zone)]
    public class SocialServicePerformanceTests : PerformanceBenchmark
    {
        private readonly DecentralandEnvironment env;
        private readonly DecentralandUrlsSource urlsSource;

        // We need an identity for Signed Fetch, assume here it is taken from Player Prefabs as we can't authenticate in Tests
        // private readonly IWeb3IdentityCache identityCache = new ProxyIdentityCache(
        //     new MemoryWeb3IdentityCache(),
        //     new PlayerPrefsIdentityProvider(
        //         new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(new Web3AccountFactory()))
        // );

        public SocialServicePerformanceTests(DecentralandEnvironment env)
        {
            this.env = env;
            urlsSource = new DecentralandUrlsSource(env, ILaunchMode.PLAY);
        }

        private string communitiesBaseUrl => urlsSource.Url(DecentralandUrl.Communities);

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 100, 0.25d, 1 },
            new object[] { 10, 50, 0.25d, 100 },
            new object[] { 50, 50, 0.25d, 100 },
            new object[] { 100, 50, 0.25d, 100 },
            new object[] { 20, 10, 6, 25 },
            new object[] { 20, 5, 20, 25 },
        };

        /// <summary>
        ///     Bypass Signed Fetch
        /// </summary>
        private WebRequestHeadersInfo AuthorizeRequest() =>
            new WebRequestHeadersInfo().Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("DCLAuthSecret", EnvironmentVariableTarget.User)}");

        [TestCase(10, 100, 0.25d, 1)] // Concurrency doesn't matter - it's always one request at a time (from the design perspective)
        [TestCase(50, 20, 0.25d, 100)] // Artificial simulation of load
        [TestCase(100, 20, 0.25d, 100)] // Artificial simulation of load
        [TestCase(10, 20, 6d, 1)]
        [Performance]
        public async Task GetCommunitiesAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            string url = $"{communitiesBaseUrl}?limit=25";

            CreateController(concurrency);

            await BenchmarkAsync(concurrency, _ => controller!.GetAsync(url, CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST, AuthorizeRequest())
                                                              .CreateFromJson<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft),
                new[] { "" }, 2, totalRequests, iterations, delay);
        }

        protected async UniTask<string[]> GetCommunitiesIdsAsync()
        {
            analytics.WarmingUp = true;

            string url = $"{communitiesBaseUrl}?limit=25";

            GetUserCommunitiesResponse? resp = await controller!.GetAsync(url, CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST, AuthorizeRequest())
                                                                .CreateFromJson<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft);

            analytics.WarmingUp = false;

            return resp.data.results.Select(r => r.id).ToArray();
        }

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Timeout(10 * 60 * 1000)]
        [Performance]
        public async Task GetCommunityByIdAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            CreateController(concurrency);

            await BenchmarkAsync(concurrency,
                id => controller!.GetAsync($"{communitiesBaseUrl}/{id}", CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST, AuthorizeRequest())
                                 .CreateFromJson<GetCommunityResponse>(WRJsonParser.Newtonsoft),
                await GetCommunitiesIdsAsync(), 1, totalRequests, iterations, delay);
        }

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Timeout(10 * 60 * 1000)]
        [Performance]
        public async Task GetCommunityPlacesAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            CreateController(concurrency);

            await BenchmarkAsync(concurrency,
                id => controller!.GetAsync($"{communitiesBaseUrl}/{id}/places", CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST, AuthorizeRequest())
                                 .CreateFromJson<GetCommunityResponse>(WRJsonParser.Newtonsoft),
                await GetCommunitiesIdsAsync(), 1, totalRequests, iterations, delay);
        }

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Timeout(10 * 60 * 1000)]
        [Performance]
        public async Task GetCommunityMembersAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            CreateController(concurrency);

            await BenchmarkAsync(concurrency,
                id => controller!.GetAsync($"{communitiesBaseUrl}/{id}/members", CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST, AuthorizeRequest())
                                 .CreateFromJson<GetCommunityResponse>(WRJsonParser.Newtonsoft),
                await GetCommunitiesIdsAsync(), 1, totalRequests, iterations, delay);
        }
    }
}
