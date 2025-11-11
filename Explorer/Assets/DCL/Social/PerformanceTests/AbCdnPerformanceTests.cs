using DCL.WebRequests;
using DCL.WebRequests.Dumper;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.SocialService.PerformanceTests
{
    public class AbCdnPerformanceTests : PerformanceBenchmark
    {
        internal static readonly string TEST_DUMP = $"{Application.dataPath + "/../TestResources/AssetBundles/ab-cdn_dump.json"}";

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 3, 0, 186 },
            new object[] { 10, 3, 0, 186 },
            new object[] { 50, 1, 0, 60 },
            new object[] { 50, 3, 0, 186 },
            new object[] { 100, 3, 0, 186 },
        };

        private WebRequestDump dump;
        private AssetBundleLoadingMutex loadingMutex;

        [SetUp]
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
            CreateController(concurrency, true);

            await BenchmarkAsync(concurrency, entry => entry.RecreateWithNoOp(controller!, loadingMutex, CancellationToken.None), dump.Entries, 0, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations), UnloadABs);
        }
    }
}
