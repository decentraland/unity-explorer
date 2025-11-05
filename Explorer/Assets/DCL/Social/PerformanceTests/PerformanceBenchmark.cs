using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics.Tests;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Diagnostics;
using Unity.PerformanceTesting;

namespace DCL.SocialService.PerformanceTests
{
    public class PerformanceBenchmark
    {
        protected SampleGroup? iterationTotalTime;

        protected IWebRequestController? controller;
        protected PerformanceTestWebRequestsAnalytics analytics;

        private MockedReportScope? reportScope;

        [SetUp]
        public void SetUpBenchmark()
        {
            iterationTotalTime = new SampleGroup("Iteration Total Time", SampleUnit.Microsecond);
            reportScope = new MockedReportScope();
        }

        [TearDown]
        public void TearDown() =>
            reportScope?.Dispose();

        public void CreateController(int concurrency)
        {
            analytics = new PerformanceTestWebRequestsAnalytics();

            controller = new BudgetedWebRequestController(new WebRequestController(analytics, Substitute.For<IWeb3IdentityCache>(),
                new RequestHub(new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY)),
                ChromeDevtoolProtocolClient.NewForTest()), concurrency, new ElementBinding<ulong>(0));
        }

        protected async UniTask BenchmarkAsync(int concurrency, Func<string, UniTask> createRequest, string[] loopThrough, int warmupCount, int targetRequestsCount,
            int iterationsCount, TimeSpan delayBetweenIterations)
        {
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
                    tasks[j] = createRequest(loopThrough[j % loopThrough.Length]).SuppressToResultAsync();

                await UniTask.WhenAll(tasks);

                Measure.Custom(iterationTotalTime, PerformanceTestWebRequestsAnalytics.ToMs(ts, Stopwatch.GetTimestamp()));

                await UniTask.Delay(delayBetweenIterations);
            }
        }
    }
}
