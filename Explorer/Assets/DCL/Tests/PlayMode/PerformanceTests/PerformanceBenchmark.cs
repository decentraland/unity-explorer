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
using System.Collections.Generic;
using System.Diagnostics;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    public class PerformanceBenchmark
    {
        protected readonly IWeb3IdentityCache identityCache = Substitute.For<IWeb3IdentityCache>();
        protected SampleGroup? iterationTotalTime;
        protected SampleGroup? iterationDownloadedData;

        protected IWebRequestController? controller;
        protected PerformanceTestWebRequestsAnalytics analytics;

        private MockedReportScope? reportScope;

        [SetUp]
        public void SetUpBenchmark()
        {
            iterationTotalTime = new SampleGroup("Iteration Total Time", SampleUnit.Microsecond);
            iterationDownloadedData = new SampleGroup("Iteration Downloaded Data", SampleUnit.Megabyte);
            reportScope = new MockedReportScope();
        }

        [TearDown]
        public void TearDown() =>
            reportScope?.Dispose();

        public void CreateController(int concurrency, bool disableABCache = false)
        {
            analytics = new PerformanceTestWebRequestsAnalytics();

            controller = new WebRequestController(analytics, identityCache,
                new RequestHub(new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY), disableABCache),
                ChromeDevtoolProtocolClient.NewForTest(), new WebRequestBudget(concurrency, new ElementBinding<ulong>(0)));
        }

        protected async UniTask BenchmarkAsync<TParam>(int concurrency, Func<TParam, UniTask> createRequest, IReadOnlyList<TParam> loopThrough, int warmupCount, int targetRequestsCount,
            int iterationsCount, TimeSpan delayBetweenIterations, Action? onIterationFinished = null)
        {
            analytics.WarmingUp = true;

            // Warmup a few times (DNS/TLS/JIT)
            for (int i = 0; i < warmupCount; i++)
                await UniTask.WhenAll(loopThrough.Select(createRequest));

            analytics.WarmingUp = false;

            for (int i = 0; i < iterationsCount; i++)
            {
                long ts = Stopwatch.GetTimestamp();

                double downloadedData = Sum(analytics.downloadedDataSize);

                // Loop though parameters to fill up all required tasks

                var tasks = new UniTask[targetRequestsCount];

                for (int j = 0; j < targetRequestsCount; j++)
                    tasks[j] = createRequest(loopThrough[j % loopThrough.Count]).SuppressToResultAsync();

                await UniTask.WhenAll(tasks);

                Measure.Custom(iterationTotalTime, PerformanceTestWebRequestsAnalytics.ToMs(ts, Stopwatch.GetTimestamp()));
                Measure.Custom(iterationDownloadedData, Sum(analytics.downloadedDataSize) - downloadedData);

                onIterationFinished?.Invoke();

                await UniTask.Delay(delayBetweenIterations);
            }

            static double Sum(SampleGroup sampleGroup)
            {
                double sum = 0.0;

                foreach (double sample in sampleGroup.Samples)
                    sum += sample;

                return sum;
            }
        }
    }
}
