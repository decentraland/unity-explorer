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
    /// <summary>
    ///     Meant to be used with "Resource Monitor" / "socket_monitor" to investigate
    ///     multiplexing over HTTP/2
    /// </summary>
    [TestFixture(DecentralandEnvironment.Org)]
    public class GatewayMultiplexingBenchmark : PerformanceBenchmark
    {
        [SetUp]
        public void ReadDump()
        {
            loadingMutex = new AssetBundleLoadingMutex();
            dump = WebRequestsDumper.Deserialize(TEST_DUMP);

            EnableErrors();
        }

        [TearDown]
        public void UnloadABs() =>
            AssetBundle.UnloadAllAssetBundles(true);

        internal static readonly string TEST_DUMP = $"{Application.dataPath + "/../TestResources/Gateway/web_requests_dump.json"}";

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 20, 1, 0, 1 },
        };

        private readonly DecentralandEnvironment env;

        private WebRequestDump dump;
        private AssetBundleLoadingMutex loadingMutex;

        public GatewayMultiplexingBenchmark(DecentralandEnvironment env)
        {
            this.env = env;
        }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task LoadFromDumpAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency, true, env, useGateway: true);

            await BenchmarkAsync(dump => dump.RecreateWithTiming(controller!, loadingMutex, CancellationToken.None), new[] { dump }, 0, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations), UnloadABs);
        }
    }
}
