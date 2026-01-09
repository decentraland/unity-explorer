using NUnit.Framework;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture("https://asset-bundle-registry.decentraland.org/")]
    [TestFixture("https://gateway.decentraland.org/asset-bundle-registry/")]
    public class AssetBundleRegistryOrgPerformanceTests : AssetBundleRegistryPerformanceBenchmarkBase
    {
        private static readonly string[] SCENES =
        {
            "7,16",
            "8,16",
            "9,16",
            "-3,17",
            "-2,17",
            "-1,17",
            "6,17",
            "7,17",
            "8,17",
            "11,-17",
            "11,-16",
            "11,-14",
            "11,-13",
            "11,-12",
            "11,-11",
            "11,-10",
            "-25,-9",
            "11,-9",
            "-25,-8",
            "11,-8",
            "11,-7",
            "11,-6",
            "11,-5",
            "11,-4",
            "11,-3",
            "11,-2",
            "11,-1",
            "11,0",
            "11,1",
            "-25,2",
            "11,2",
            "11,3",
            "11,4",
            "-25,5",
            "11,5",
            "11,6",
            "-25,7",
            "11,7",
            "11,8",
            "11,9",
            "11,10",
            "-25,11",
            "11,11",
            "-25,12",
            "-25,14",
            "11,17",
            "-25,18",
            "-24,18",
            "-23,18",
            "-20,18",
            "-16,18",
            "-6,18",
            "6,18",
            "8,18",
            "11,18",
            "-26,-19",
            "-17,-19",
            "-16,-19",
            "3,-19",
            "4,-19",
        };

        private static readonly string[] WEARABLES =
        {
            "urn:decentraland:matic:collections-v2:0xd50191baed16bc532feb9d499fdaa805fe01d3ff:8",
            "urn:decentraland:matic:collections-v2:0x4e91726416b4e3fe69c08b92f312050f39bfdae5:0",
            "urn:decentraland:off-chain:base-avatars:eyes_06",
            "urn:decentraland:off-chain:base-avatars:f_roller_leggings",
            "urn:decentraland:off-chain:base-avatars:cord_bracelet",
            "urn:decentraland:matic:collections-v2:0x66871d01e15af85ea6c172b7c4821b0f9bb71880:0",
            "urn:decentraland:off-chain:base-avatars:pink_blue_socks",
            "urn:decentraland:matic:collections-v2:0x996b51131698ba70dcfb3fb5956e6816c5778eda:0",
        };

        public AssetBundleRegistryOrgPerformanceTests(string assetBundleRegistryUrl) : base(assetBundleRegistryUrl) { }

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public Task GetScenesEntities(int concurrency, int iterations, double delayBetweenIterations, int totalRequests) =>
            GetEntitiesActiveAsync(concurrency, iterations, delayBetweenIterations, totalRequests, SCENES);

        [Test]
        [Performance]
        [Timeout(10 * 60 * 1000)]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public Task GetWearablesEntities(int concurrency, int iterations, double delayBetweenIterations, int totalRequests) =>
            GetEntitiesActiveAsync(concurrency, iterations, delayBetweenIterations, totalRequests, WEARABLES);
    }
}
