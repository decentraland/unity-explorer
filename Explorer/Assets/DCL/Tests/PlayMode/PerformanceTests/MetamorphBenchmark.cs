using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using DCL.WebRequests.Dumper;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture(DecentralandEnvironment.Org)]
    public class MetamorphBenchmark : PerformanceBenchmark
    {
        internal static readonly string TEST_DUMP = $"{Application.dataPath + "/../TestResources/Images/metamorph_map_dump.json"}";

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 10, 3, 0, 100 },
            new object[] { 50, 2, 0, 100 },
        };

        private readonly DecentralandEnvironment env;

        private WebRequestDump dump;
        private AssetBundleLoadingMutex loadingMutex;

        public MetamorphBenchmark(DecentralandEnvironment env)
        {
            this.env = env;
        }

        [OneTimeSetUp]
        public void ReadDump()
        {
            loadingMutex = new AssetBundleLoadingMutex();
            dump = WebRequestsDumper.Deserialize(TEST_DUMP);
        }

        [TearDown]
        public void UnloadABs() =>
            AssetBundle.UnloadAllAssetBundles(true);

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task LoadFromDumpAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency, true, env);

            await BenchmarkAsync(entry => entry.RecreateWithNoOp(controller!, loadingMutex, CancellationToken.None), dump.Entries, 0, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations), UnloadABs);
        }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task LoadFromDumpViaGatewayAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency, true, env, useGateway: true);

            await BenchmarkAsync(entry => entry.RecreateWithNoOp(controller!, loadingMutex, CancellationToken.None), dump.Entries, 0, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations), UnloadABs);
        }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task LoadFromDumpWithTimingsAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency, true, env);

            await BenchmarkAsync(dump => dump.RecreateWithTiming(controller!, loadingMutex, CancellationToken.None), new[] { dump }, 0, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations), UnloadABs);
        }
    }
}
