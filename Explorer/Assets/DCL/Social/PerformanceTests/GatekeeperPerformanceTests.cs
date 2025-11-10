using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.SocialService.PerformanceTests
{
    [TestFixture("https://comms-gatekeeper.decentraland.zone/")]
    [TestFixture("https://comms-gatekeeper-test.decentraland.zone/")]
    public class GatekeeperPerformanceTests : PerformanceBenchmark
    {
        [Serializable]
        public class RealmMetadata
        {
            public string hostname;
            public string protocol;
            public string serverName;
        }

        [Serializable]
        public class RequestMetadata
        {
            public string signer;
            public RealmMetadata realm;
            public string sceneId;
            public string parcel;
        }

        [Serializable]
        public class TestScenario
        {
            public string name;
            public string description;
            public RealmMetadata realm;
            public string sceneId;
            public string parcel;
            public bool isWorld;
        }

        private static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 100, 0.25d, 1 },
            new object[] { 1, 20, 0.25d, 100 },
            new object[] { 10, 20, 0.25d, 100 },
            new object[] { 50, 20, 0.25d, 100 },
            new object[] { 100, 20, 0.25d, 100 },
            new object[] { 20, 10, 6, 25 },
            new object[] { 20, 5, 20, 25 },
        };

        private readonly string gatekeeperUrl;
        private readonly string sceneAdminUrl;

        public GatekeeperPerformanceTests(string gatekeeperUrl)
        {
            this.gatekeeperUrl = gatekeeperUrl;
            sceneAdminUrl = gatekeeperUrl + "scene-admin";

            IWeb3Identity identity = PrivateKeyAuthenticator.Login(Environment.GetEnvironmentVariable("DCLBenchmarkPKey", EnvironmentVariableTarget.User)!);

            identityCache.Identity.Returns(identity);
        }

        private static TestScenario CreateWorldScenario(string worldName) =>
            new ()
            {
                name = $"World: {worldName}",
                description = $"Testing world {worldName}",
                sceneId = "bafkreiworld0",
                parcel = "0,0",
                isWorld = true,
                realm = new RealmMetadata
                {
                    hostname = "https://worlds-content-server.decentraland.org",
                    protocol = "https",
                    serverName = worldName.Trim(),
                },
            };

        [Test]
        [Performance]
        [TestCaseSource(nameof(TEST_CASES_SOURCE))]
        public async Task WorldSceneAdminAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            const string WORLD = "palealetest.dcl.eth";

            await SceneAdminAsync(new[] { CreateWorldScenario(WORLD) }, concurrency, iterations, delayBetweenIterations, totalRequests);
        }

        private Task SceneAdminAsync(TestScenario[] scenarios, int concurrency, int iterations, double delayBetweenIterations, int totalRequests)
        {
            CreateController(concurrency);

            return BenchmarkAsync(concurrency, param =>
                {
                    var metadata = new RequestMetadata
                    {
                        signer = "decentraland-kernel-scene",
                        realm = param.realm,
                        sceneId = param.sceneId,
                        parcel = param.parcel,
                    };

                    return controller!.SignedFetchGetAsync(sceneAdminUrl, JsonUtility.ToJson(metadata), CancellationToken.None).WithNoOpAsync();
                }, scenarios, 1, totalRequests, iterations, TimeSpan.FromSeconds(delayBetweenIterations))
               .AsTask();
        }
    }
}
