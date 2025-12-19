using DCL.Diagnostics;
using DCL.Profiles;
using DCL.WebRequests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture("https://peer-ec1.decentraland.org/lambdas/")]
    [TestFixture("https://peer-ec2.decentraland.org/lambdas/")]
    [TestFixture("https://peer-ap1.decentraland.org/lambdas/")]
    [TestFixture("https://asset-bundle-registry.decentraland.today/")]
    public class ProfilesPerformanceTest : PerformanceBenchmark
    {
        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 5, 0.25d, 100 },
            new object[] { 10, 10, 0.25d, 100 },
            new object[] { 50, 10, 0.25d, 100 },
            new object[] { 100, 10, 0.25d, 100 },
            new object[] { 20, 10, 6, 25 },
            new object[] { 20, 5, 20, 25 },
        };

        private const string BODY = @"{""ids"":[
    ""0x0c678c84cc5744f2a5b368ce2aeab3905624ff99"",
    ""0x755faf85277b848ed6a290c268e95e7b510ac8ff"",
    ""0x21103f779e3e69dcadfc78c0d472ad6cc591fa7b"",
    ""0x516323afcd3ec36ba309f54d05cdf6fb48fa993b"",
    ""0x05de05303eab867d51854e8b4fe03f7acb0624d9"",
    ""0x196bb73dabc6465f7f8cd8d26c5c7383a395694e"",
    ""0xc81f875d23e9de99018fd109178a4856b1dd5e42"",
    ""0x6bb7a5acab90a40161ee43b094460ba621dfb47f"",
    ""0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1"",
    ""0x0ad3a9dea3221930c9a1c0a59c1597536681521d"",
    ""0xcd4ea8e05945f34122679f5035cd6014f3263863"",
    ""0x7ba641833a2925d71046351f97a92235dc777616"",
    ""0xa23aa5fce659a828ab52d62a708e29e3347b9eb7"",
    ""0x762fba3875baf5dfa20bfc05588ed377dc739f9a"",
    ""0xd736144c39dac0122d70a2ca6b1725a67b0fc00b"",
    ""0xce355d42efc49b840c1a796ea678655fdd43498c"",
    ""0xf934072898cb464b17d6ff8a380942c88964b3b5"",
    ""0x3de5a5177f1fdcb77ca5c56c1eb5b3e7ab141c12"",
    ""0x82f2b3705cd21501a9cd908814bf1c32942f42e1""
    ]}";

        private readonly string profilesUrls;

        public ProfilesPerformanceTest(string lambdas)
        {
            profilesUrls = lambdas + "profiles";
        }

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Performance]
        [Test]
        public async Task PostProfilesAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency);

            await BenchmarkAsync(concurrency, _ => controller!.PostAsync(profilesUrls, GenericPostArguments.CreateJson(BODY), CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                                              .CreateFromJson<List<GetProfileJsonRootDto>>(WRJsonParser.Newtonsoft), new[] { "" }, 1, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations));
        }
    }
}
