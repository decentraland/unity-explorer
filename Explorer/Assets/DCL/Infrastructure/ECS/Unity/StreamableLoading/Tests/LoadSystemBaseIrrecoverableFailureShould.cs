using Arch.Core;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Tests
{
    /// <summary>
    ///     Covers the <c>IrrecoverableFailures</c> poisoning fix. Pre-fix, <c>LoadSystemBase.RepeatLoopAsync</c>
    ///     cached ANY concluded failure — including a transient/general exception with nothing to do with a
    ///     permanent HTTP status (an aborted request, a corrupt/partial download) — into
    ///     <see cref="IStreamableCache{TAsset,TLoadingIntention}.IrrecoverableFailures"/>. Every later request for
    ///     the same intention then short-circuited at the <c>cache.IrrecoverableFailures</c> lookup in
    ///     <c>LoadSystemBase.FlowAsync</c> and failed instantly without ever reaching the load flow again. That is
    ///     what turned one transient hiccup (e.g. loading the Genesis Plaza fishing-rod asset bundle under memory
    ///     pressure) into a session-permanent, all-players failure. The fix restricts caching to a definitive HTTP
    ///     client error only (a <see cref="UnityWebRequestException"/> whose <c>ResponseCode</c> is a genuine 4xx,
    ///     per <see cref="WebRequestUtils.IsIrrecoverableError"/>); everything else stays uncached.
    /// </summary>
    [TestFixture]
    public class LoadSystemBaseIrrecoverableFailureShould
    {
        [Test]
        public async Task NotPoisonTheIntentionWhenTheLoadThrowsATransientException()
        {
            // Arrange
            using var mockedReportScope = new MockedReportScope();

            var world = World.Create();
            world.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

            var cache = new RecordingCache();

            var system = new TransientFailureTestLoadSystem(world, cache) { FailuresLeft = 1 };
            system.Initialize();

            try
            {
                var promiseA = AssetPromise<StubAsset, StubIntention>.Create(world, NewIntention(), PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promiseA.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

                // Act — FlowInternalAsync throws a plain, non-web exception on its first invocation, standing in
                // for a corrupt/partial asset-bundle download or any other transient failure unrelated to a
                // permanent HTTP status
                system.Update(0);

                for (var i = 0; i < 10; i++)
                    await UniTask.Yield();

                // Assert — the promise concluded with a failure...
                Assert.That(world.Has<StreamableLoadingResult<StubAsset>>(promiseA.Entity), Is.True,
                    "The first promise should be finalized with a result");

                StreamableLoadingResult<StubAsset> resultA = world.Get<StreamableLoadingResult<StubAsset>>(promiseA.Entity);
                Assert.That(resultA.Succeeded, Is.False, "The first promise should have failed");

                // ...but the transient exception must NOT poison the intention for the rest of the session
                Assert.That(cache.IrrecoverableFailures.Count, Is.Zero,
                    "A general/transient exception (not a permanent 4xx web failure) must not be cached as "
                    + "irrecoverable, otherwise every later load of the same asset fails instantly forever");

                Assert.That(system.FlowInternalCallCount, Is.EqualTo(1));

                // Arrange — a second promise for the same intention; this time FlowInternalAsync succeeds
                var promiseB = AssetPromise<StubAsset, StubIntention>.Create(world, NewIntention(), PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promiseB.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

                // Act
                system.Update(0);

                for (var i = 0; i < 10; i++)
                    await UniTask.Yield();

                // Assert — the second attempt actually re-entered the load flow (proving it was not
                // short-circuited by a poisoned IrrecoverableFailures entry) and succeeded
                Assert.That(system.FlowInternalCallCount, Is.EqualTo(2),
                    "The second promise must re-enter FlowInternalAsync; if the first transient failure had "
                    + "poisoned IrrecoverableFailures, LoadSystemBase.FlowAsync would have short-circuited at "
                    + "the cache.IrrecoverableFailures lookup and never invoked the flow again");

                Assert.That(world.Has<StreamableLoadingResult<StubAsset>>(promiseB.Entity), Is.True,
                    "The second promise should be finalized");

                Assert.That(world.Get<StreamableLoadingResult<StubAsset>>(promiseB.Entity).Succeeded, Is.True,
                    "The second, retried attempt should succeed since the transient condition has cleared");
            }
            finally
            {
                system.Dispose();
                world.Dispose();
            }
        }

        [Test]
        public async Task RecordAsIrrecoverableWhenTheLoadFailsWithADefinitive404()
        {
            // Arrange
            using var mockedReportScope = new MockedReportScope();

            var world = World.Create();
            world.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

            var cache = new RecordingCache();

            int port = FreeLoopbackPort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            Task serverTask = ServeOneNotFoundResponseAsync(listener);

            var system = new WebRequestTestLoadSystem(world, cache);
            system.Initialize();

            try
            {
                var intention = new StubIntention
                {
                    CommonArguments = new CommonLoadingArguments($"http://127.0.0.1:{port}/missing-asset-bundle", attempts: 1),
                };

                var promise = AssetPromise<StubAsset, StubIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

                // Act — a real UnityWebRequest is sent to the local listener, which answers 404 Not Found; this
                // reaches AssetsLoadingUtility.RepeatLoopAsync as a genuine UnityWebRequestException, exactly like
                // production asset-bundle/texture loaders
                system.Update(0);

                DateTime deadline = DateTime.UtcNow.AddSeconds(10);

                // Plain Task.Delay (not UniTask.Delay) on purpose: EditMode tests do not run the Player Loop in
                // "playing" state, so a frame-time-based delay is not guaranteed to elapse; a real-time delay is.
                while (!world.Has<StreamableLoadingResult<StubAsset>>(promise.Entity) && DateTime.UtcNow < deadline)
                    await Task.Delay(50);

                // Assert
                Assert.That(world.Has<StreamableLoadingResult<StubAsset>>(promise.Entity), Is.True,
                    "The promise should be finalized with a result");

                StreamableLoadingResult<StubAsset> result = world.Get<StreamableLoadingResult<StubAsset>>(promise.Entity);
                Assert.That(result.Succeeded, Is.False, "The request should have failed with a 404");

                Assert.That(cache.IrrecoverableFailures.Count, Is.EqualTo(1),
                    "A genuine, definitive 4xx failure (per WebRequestUtils.IsIrrecoverableError) must still be "
                    + "cached as irrecoverable — the fix only stops caching transient/general failures");
            }
            finally
            {
                system.Dispose();
                world.Dispose();
                listener.Stop();
                listener.Close();

                // Stopping the listener aborts a still-pending GetContextAsync with an exception; irrelevant to
                // the assertions above, just drain it so it doesn't surface as an unobserved task exception.
                try { await serverTask; }
                catch (Exception) { }
            }
        }

        private static async Task ServeOneNotFoundResponseAsync(HttpListener listener)
        {
            HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return freePort;
        }

        private static StubIntention NewIntention() =>
            new ()
            {
                CommonArguments = new CommonLoadingArguments("http://test/asset", attempts: 1),
            };

        private class StubAsset { }

        private struct StubIntention : ILoadingIntention, IEquatable<StubIntention>
        {
            public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
            public CommonLoadingArguments CommonArguments { get; set; }

            public bool Equals(StubIntention other) =>
                true;

            public override bool Equals(object obj) =>
                obj is StubIntention other && Equals(other);

            public override int GetHashCode() =>
                0;
        }

        /// <summary>
        ///     A minimal cache with real backing dictionaries for <see cref="OngoingRequests"/> and
        ///     <see cref="IrrecoverableFailures"/>, so the test can assert on their contents directly rather than
        ///     mock them through NSubstitute.
        /// </summary>
        private class RecordingCache : IStreamableCache<StubAsset, StubIntention>
        {
            public IDictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<StubAsset>>> OngoingRequests { get; } =
                new Dictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<StubAsset>>>();

            public IDictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, StreamableLoadingResult<StubAsset>?> IrrecoverableFailures { get; } =
                new Dictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, StreamableLoadingResult<StubAsset>?>();

            public void Dispose() { }

            public bool TryGet(in StubIntention key, out StubAsset asset)
            {
                asset = null!;
                return false;
            }

            public void Add(in StubIntention key, StubAsset asset) { }

            public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }
        }

        /// <summary>
        ///     Fails once with a plain, non-web exception (standing in for a corrupt/partial asset-bundle
        ///     download), then succeeds — lets the test prove the first failure does not block the second attempt.
        /// </summary>
        private class TransientFailureTestLoadSystem : LoadSystemBase<StubAsset, StubIntention>
        {
            public int FailuresLeft;
            public int FlowInternalCallCount { get; private set; }

            internal TransientFailureTestLoadSystem(World world, IStreamableCache<StubAsset, StubIntention> cache) : base(world, cache) { }

            protected override UniTask<StreamableLoadingResult<StubAsset>> FlowInternalAsync(StubIntention intention,
                StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
            {
                FlowInternalCallCount++;

                if (FailuresLeft > 0)
                {
                    FailuresLeft--;
                    throw new InvalidOperationException("Simulated transient failure (e.g. a corrupt/partial asset-bundle download)");
                }

                return UniTask.FromResult(new StreamableLoadingResult<StubAsset>(new StubAsset()));
            }

            // BaseUnityLoopSystem relies on a source-generated attribute info for runtime metadata. The generator only
            // emits it for non-nested, partial systems; this test helper is nested and has no attributes, so return
            // null and let BaseUnityLoopSystem.GetReportCategory fall back to ReportCategory.ECS.
            protected override AttributesInfoBase GetMetadataInternal() => null!;
        }

        /// <summary>
        ///     Issues a real <see cref="UnityWebRequest"/> so a genuine <see cref="UnityWebRequestException"/> with
        ///     a real <c>ResponseCode</c> reaches <c>AssetsLoadingUtility.RepeatLoopAsync</c>, exactly like the
        ///     production asset-bundle/texture/etc. loaders do.
        /// </summary>
        private class WebRequestTestLoadSystem : LoadSystemBase<StubAsset, StubIntention>
        {
            internal WebRequestTestLoadSystem(World world, IStreamableCache<StubAsset, StubIntention> cache) : base(world, cache) { }

            protected override async UniTask<StreamableLoadingResult<StubAsset>> FlowInternalAsync(StubIntention intention,
                StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest request = UnityWebRequest.Get(intention.CommonArguments.URL);
                await request.SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<StubAsset>(new StubAsset());
            }

            protected override AttributesInfoBase GetMetadataInternal() => null!;
        }
    }
}
