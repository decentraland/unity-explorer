using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SocialService;
using DCL.Utilities.Extensions;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    public abstract class RPCSocialServiceBenchmarkBase : PerformanceBenchmark
    {
        private const string PKEY_VAR_NAME = "DCLBenchmarkCIPKey";

        private readonly RPCSocialServices core;
        protected PerformanceTestSocialService socialService;

        protected RPCSocialServiceBenchmarkBase(DecentralandEnvironment env)
        {
            var address = URLAddress.FromString($"wss://rpc-social-service-ea.decentraland.{env}");

            string? ciPkey = Environment.GetEnvironmentVariable(PKEY_VAR_NAME);

            if (string.IsNullOrEmpty(ciPkey))
                ciPkey = Environment.GetEnvironmentVariable(PKEY_VAR_NAME, EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(ciPkey))
            {
                // Parse CLI arguments as a last resort
                string[] args = Environment.GetCommandLineArgs();

                string cliKey = $"-{PKEY_VAR_NAME}";

                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == cliKey)
                    {
                        ciPkey = args[i + 1];
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(ciPkey))
                throw new ArgumentException("DCLBenchmarkCIPKey", "Private key is null/empty or not provided");

            ciPkey = ciPkey.Trim();

            IWeb3Identity identity = PrivateKeyAuthenticator.Login(ciPkey);
            identityCache.Identity.Returns(identity);

            core = new RPCSocialServices(address, identityCache, Substitute.For<ISocialServiceEventBus>());
        }

        [SetUp]
        public Task ConnectAsync()
        {
            socialService = new PerformanceTestSocialService(core);
            return core.EnsureRpcConnectionAsync(3, CancellationToken.None).AsTask();
        }

        [TearDown]
        public Task DisconnectAsync() =>
            core.DisconnectAsync(CancellationToken.None).AsTask();

        protected async UniTask BenchmarkAsync<TParam>(int concurrency, Func<TParam, UniTask> createRequest, IReadOnlyList<TParam> loopThrough, int warmupCount, int targetRequestsCount,
            int iterationsCount, TimeSpan delayBetweenIterations, Action? onIterationFinished = null)
        {
            socialService.WarmingUp = true;

            // Warmup a few times (DNS/TLS/JIT)
            for (int i = 0; i < warmupCount; i++)
                await UniTask.WhenAll(loopThrough.Select(createRequest));

            socialService.WarmingUp = false;

            var budget = new WebRequestBudget(concurrency, new ElementBinding<ulong>(0));

            for (int i = 0; i < iterationsCount; i++)
            {
                long ts = Stopwatch.GetTimestamp();

                // Loop though parameters to fill up all required tasks

                var tasks = new UniTask[targetRequestsCount];

                for (int j = 0; j < targetRequestsCount; j++)
                    tasks[j] = WithBudget(createRequest(loopThrough[j % loopThrough.Count])).SuppressToResultAsync();

                await UniTask.WhenAll(tasks);

                Measure.Custom(iterationTotalTime, PerformanceTestWebRequestsAnalytics.ToMs(ts, Stopwatch.GetTimestamp()));

                onIterationFinished?.Invoke();

                await UniTask.Delay(delayBetweenIterations);
            }

            async UniTask WithBudget(UniTask coreTask)
            {
                using (await budget.AcquireAsync(CancellationToken.None)) await coreTask;
            }
        }
    }
}
