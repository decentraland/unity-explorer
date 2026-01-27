using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles.Self;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    [TestFixture(DecentralandEnvironment.Org)]
    public class RPCFriendsServiceBenchmark : RPCSocialServiceBenchmarkBase
    {
        [SetUp]
        public void CreateService() =>
            friendsService = new RPCFriendsService(Substitute.For<IFriendsEventBus>(), new FriendsCache(), Substitute.For<ISelfProfile>(), socialService);

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 5, 0.25d, 100 },
            new object[] { 10, 5, 0.25d, 100 },
            new object[] { 50, 5, 0.25d, 100 },
            new object[] { 100, 5, 0.25d, 100 },
            new object[] { 20, 5, 6, 25 },
            new object[] { 20, 5, 20, 25 },
        };

        private static readonly string[] NO_PARAMS = { string.Empty };

        private RPCFriendsService friendsService;

        public RPCFriendsServiceBenchmark(DecentralandEnvironment env) : base(env) { }

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Performance]
        [Test]
        public Task GetFriendsAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests) =>
            BenchmarkAsync(concurrency, _ => friendsService.GetFriendsAsync(0, 100, CancellationToken.None),
                    NO_PARAMS, 1, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations))
               .AsTask();

        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        [Performance]
        [Test]
        public Task GetBlockedUsersAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests) =>
            BenchmarkAsync(concurrency, _ => friendsService.GetBlockedUsersAsync(0, 100, CancellationToken.None),
                    NO_PARAMS, 1, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations))
               .AsTask();
    }
}
