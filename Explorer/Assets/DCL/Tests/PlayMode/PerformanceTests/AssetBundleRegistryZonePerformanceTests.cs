using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.WebRequests;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    ///     Can't compare org and zone directly as they contain different points
    /// </summary>
    [TestFixture("https://asset-bundle-registry.decentraland.zone/")]
    [TestFixture("https://asset-bundle-registry-test.decentraland.zone/")]
    [TestFixture("https://gateway.decentraland.zone/asset-bundle-registry/")]
    public class AssetBundleRegistryZonePerformanceTests : AssetBundleRegistryPerformanceBenchmarkBase
    {
        private const string ENTITIES_STATUS = "entities/status/";

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

        private readonly URLAddress entitiesStatus;

        private IWebRequestController? webRequestController;

        public AssetBundleRegistryZonePerformanceTests(string assetBundleRegistryUrl) : base(assetBundleRegistryUrl)
        {
            entitiesStatus = URLAddress.FromString(assetBundleRegistryUrl + ENTITIES_STATUS);
        }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public Task GetEntitiesActive(int concurrency, int iterations, double delayBetweenIterations, int totalRequests) =>
            GetEntitiesActiveAsync(concurrency, iterations, delayBetweenIterations, totalRequests, TEST_POINTERS);

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task GetEntitiesStatusAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            CreateController(concurrency);

            await BenchmarkAsync(id =>
                    webRequestController!.GetAsync(new CommonArguments(URLAddress.FromString(entitiesStatus.Value + id)), CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                         .WithNoOpAsync(),
                TEST_ENTITY_IDS,
                3, totalRequests, iterations, delay);
        }
    }
}
