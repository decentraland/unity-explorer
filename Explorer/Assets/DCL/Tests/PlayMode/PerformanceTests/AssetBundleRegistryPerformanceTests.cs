using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture("https://asset-bundle-registry.decentraland.zone/")]
    [TestFixture("https://asset-bundle-registry-test.decentraland.zone/")]
    public class AssetBundleRegistryPerformanceTests
    {
        [SetUp]
        public void SetUp()
        {
            iterationTotalTime = new SampleGroup("Iteration Total Time", SampleUnit.Microsecond);

            reportScope = new MockedReportScope();
        }

        [TearDown]
        public void TearDown() =>
            reportScope?.Dispose();

        [Serializable]
        public class GenericEntityDefinition : EntityDefinitionBase { }

        private const string ENTITIES_STATUS = "entities/status/";
        private const string ENTITIES_ACTIVE = "entities/active/";

        private static readonly string[] TEST_POINTERS =
        {
            "urn:decentraland:amoy:collections-v2:0x7cd302743fd16f124ede95968a1e84f4c860cc3c:0",
            "urn:decentraland:amoy:collections-v2:0xe68c6bb40a040c6b15558c6a44683977521cdf96:0",
            "alelevyyyyy.dcl.eth",
            "Kazzu.dcl.eth",
            "buenardo.dcl.eth",
            "cubes.dcl.eth",
            "20,6",
            "urn:decentraland:amoy:collections-v2:0xa64c2e6b8dda32a6ee7f629207f47017550e386d:1",
        };

        private static readonly string[] TEST_ENTITY_IDS =
        {
            "bafkreierf77w4cibrc2hzymne52qedc7kd75i56xges445aqc76qwi4y6y",
            "bafkreid7cqbkb6kp3g3m623humsym7ww2hpaaoh5iniepqkit44upx3nmi",
            "bafkreidzesbqicgchyvdck2u32s7sn3z2dptgzf3m45ye5m32xwrydn2bi",
            "bafkreig7ow5mzrn7rguu56pmtf3725puz6hh3rxggimd3yjr4fnrgzhubq",
            "bafkreidb5axfzqcwacbggguhdxyomcdcmwzbumf3glxpb33yunwdhhausq",
            "bafkreihkzzfzm6e76oeohtd5qt3zchhhxgsnzbmd3gwmyfh7ujcyv4vj7e",
            "bafkreigwcw2t2nbr4oh25hz42qmbblfjrbrqr6f6oiq6ca2yh2fkhzx4wa",
            "bafkreiedad6rd3g7nlwpdoeea2z5kazvnjzbbrlrtere32wffhndio2kia",
            "bafkreiepnlkyjxfhxzu3crad5v45dx2wl7pnhq2q7ijk2vyj4fjxgithrm",
            "bafkreia54l64yzz7pyj2m2vmuohludgtyvxrofcgxxwmlny3fx2rkleota",
            "bafkreievwo6tdx2sjybht2hektq5ifwkm7pzglo5ow4lorkud4pbutfmbu",
        };

        private readonly URLAddress entitiesActive;
        private readonly URLAddress entitiesStatus;

        private PerformanceTestWebRequestsAnalytics? analytics;

        private IWebRequestController? webRequestController;

        private MockedReportScope? reportScope;

        private SampleGroup? iterationTotalTime;

        public AssetBundleRegistryPerformanceTests(string assetBundleRegistryUrl)
        {
            entitiesActive = URLAddress.FromString(assetBundleRegistryUrl + ENTITIES_ACTIVE);
            entitiesStatus = URLAddress.FromString(assetBundleRegistryUrl + ENTITIES_STATUS);
        }

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 10, 10, 0.25d, 100 },
            new object[] { 25, 10, 0.25d, 100 },
            new object[] { 50, 10, 0.25d, 100 },
            new object[] { 20, 10, 6, 20 },
            new object[] { 20, 5, 20, 20 },
        };

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task GetEntitiesActive(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            await BenchmarkAsync(concurrency, _ =>
            {
                var bodyBuilder = new StringBuilder();

                bodyBuilder.Append("{\"pointers\":[");

                for (int i = 0; i < TEST_POINTERS.Length; ++i)
                {
                    string pointer = TEST_POINTERS[i];

                    // String Builder has overloads for int to prevent allocations
                    bodyBuilder.Append('\"');
                    bodyBuilder.Append(pointer);
                    bodyBuilder.Append(',');
                    bodyBuilder.Append(pointer);
                    bodyBuilder.Append('\"');

                    if (i != TEST_POINTERS.Length - 1)
                        bodyBuilder.Append(",");
                }

                bodyBuilder.Append("]}");

                return webRequestController!.PostAsync(new CommonArguments(entitiesActive, RetryPolicy.NONE),
                                                 GenericPostArguments.CreateJson(bodyBuilder.ToString()), CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                            .CreateFromJson<List<GenericEntityDefinition>>(WRJsonParser.Newtonsoft);
            }, new[] { "" }, 1, totalRequests, iterations, delay);
        }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task GetEntitiesStatusAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            await BenchmarkAsync(concurrency, id =>
                    webRequestController!.GetAsync(new CommonArguments(URLAddress.FromString(entitiesStatus.Value + id)), CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                         .WithNoOpAsync(),
                TEST_ENTITY_IDS,
                3, totalRequests, iterations, delay);
        }

        private async UniTask BenchmarkAsync(int concurrency, Func<string, UniTask> createRequest, string[] loopThrough, int warmupCount, int targetRequestsCount,
            int iterationsCount,
            TimeSpan delayBetweenIterations)
        {
            webRequestController = new BudgetedWebRequestController(new WebRequestController(analytics = new PerformanceTestWebRequestsAnalytics(),
                new IWeb3IdentityCache.Fake(),
                new RequestHub(new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY)),
                ChromeDevtoolProtocolClient.NewForTest()), concurrency, new ElementBinding<ulong>(0));

            analytics.WarmingUp = true;

            // Warmup a few times (DNS/TLS/JIT)
            for (int i = 0; i < warmupCount; i++) { await UniTask.WhenAll(loopThrough.Select(createRequest)); }

            analytics.WarmingUp = false;

            for (int i = 0; i < iterationsCount; i++)
            {
                long ts = Stopwatch.GetTimestamp();

                // Loop though parameters to fill up all required tasks

                var tasks = new UniTask[targetRequestsCount];

                for (int j = 0; j < targetRequestsCount; j++)
                    tasks[j] = createRequest(loopThrough[j % loopThrough.Length]);

                await UniTask.WhenAll(tasks);

                Measure.Custom(iterationTotalTime, PerformanceTestWebRequestsAnalytics.ToMs(ts, Stopwatch.GetTimestamp()));

                await UniTask.Delay(delayBetweenIterations);
            }
        }
    }
}
