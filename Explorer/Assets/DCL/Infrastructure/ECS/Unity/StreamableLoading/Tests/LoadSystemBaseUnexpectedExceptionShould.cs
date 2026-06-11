using Arch.Core;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    /// <summary>
    ///     Covers the regression where an unexpected (non-cancellation) exception thrown inside
    ///     <c>CacheableFlowAsync</c> — e.g. a corrupt disk-cache entry failing to deserialize — left its
    ///     completion source dangling in <c>OngoingRequests</c>, making every subsequent request for the
    ///     same intention await it forever without ever reaching the network.
    /// </summary>
    [TestFixture]
    public class LoadSystemBaseUnexpectedExceptionShould
    {
        [Test]
        public async Task NotLeaveDanglingOngoingRequestWhenCacheReadThrows()
        {
            // Arrange
            using var mockedReportScope = new MockedReportScope();

            var world = World.Create();
            world.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

            var cache = new ThrowingCache { ThrowsLeft = 1 };

            var system = new TestLoadSystem(world, cache);
            system.Initialize();

            try
            {
                var promiseA = AssetPromise<StubAsset, StubIntention>.Create(world, NewIntention(), PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promiseA.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

                // Act — the flow hits the throwing cache read (stands in for a corrupt disk-cache entry)
                system.Update(0);

                for (var i = 0; i < 10; i++)
                    await UniTask.Yield();

                // Assert — the promise concluded with a failure instead of staying pending
                Assert.That(world.Has<StreamableLoadingResult<StubAsset>>(promiseA.Entity), Is.True,
                    "The first promise should be finalized with a result");

                var resultA = world.Get<StreamableLoadingResult<StubAsset>>(promiseA.Entity);

                Assert.That(resultA.Succeeded, Is.False,
                    "The first promise should have failed");

                Assert.That(cache.OngoingRequests.Count, Is.Zero,
                    "The completion source must be removed from OngoingRequests on any exception, "
                    + "otherwise every subsequent request for the same intention awaits it forever");

                Assert.That(cache.IrrecoverableFailures.Count, Is.Zero,
                    "A failed cache read is not a download failure and must not be cached as irrecoverable");

                // Arrange — a new request for the same intention; this time the cache read misses normally
                // and the flow is expected to proceed to FlowInternalAsync
                system.Gate.TrySetResult(new StreamableLoadingResult<StubAsset>(new StubAsset()));

                var promiseB = AssetPromise<StubAsset, StubIntention>.Create(world, NewIntention(), PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promiseB.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

                // Act
                system.Update(0);

                for (var i = 0; i < 10; i++)
                    await UniTask.Yield();

                // Assert — the second request was not blocked by the first failure
                Assert.That(world.Has<StreamableLoadingResult<StubAsset>>(promiseB.Entity), Is.True,
                    "The second promise should be finalized and not hang on the dangling ongoing request");

                Assert.That(world.Get<StreamableLoadingResult<StubAsset>>(promiseB.Entity).Succeeded, Is.True,
                    "The second promise should succeed through the regular download flow");

                Assert.That(cache.OngoingRequests.Count, Is.Zero);
            }
            finally
            {
                system.Dispose();
                world.Dispose();
            }
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
        ///     Throws from <see cref="TryGet" /> a configurable number of times to simulate an unexpected
        ///     exception escaping the cache layer (the memory cache wraps it synchronously, so the throw
        ///     surfaces inside <c>CacheableFlowAsync</c>'s cache read).
        /// </summary>
        private class ThrowingCache : IStreamableCache<StubAsset, StubIntention>
        {
            public int ThrowsLeft;

            public IDictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<StubAsset>>> OngoingRequests { get; } =
                new Dictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<StubAsset>>>();

            public IDictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, StreamableLoadingResult<StubAsset>?> IrrecoverableFailures { get; } =
                new Dictionary<IntentionsComparer<StubIntention>.SourcedIntentionId, StreamableLoadingResult<StubAsset>?>();

            public void Dispose() { }

            public bool TryGet(in StubIntention key, out StubAsset asset)
            {
                if (ThrowsLeft > 0)
                {
                    ThrowsLeft--;
                    throw new InvalidOperationException("Corrupt cache entry");
                }

                asset = null!;
                return false;
            }

            public void Add(in StubIntention key, StubAsset asset) { }

            public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }
        }

        private class TestLoadSystem : LoadSystemBase<StubAsset, StubIntention>
        {
            public readonly UniTaskCompletionSource<StreamableLoadingResult<StubAsset>> Gate = new ();

            internal TestLoadSystem(World world, IStreamableCache<StubAsset, StubIntention> cache) : base(world, cache) { }

            protected override async UniTask<StreamableLoadingResult<StubAsset>> FlowInternalAsync(StubIntention intention,
                StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct) =>
                await Gate.Task;

            // BaseUnityLoopSystem relies on a source-generated attribute info for runtime metadata. The generator only
            // emits it for non-nested, partial systems; this test helper is nested and has no attributes, so return
            // null and let BaseUnityLoopSystem.GetReportCategory fall back to ReportCategory.ECS.
            protected override AttributesInfoBase GetMetadataInternal() => null!;
        }
    }
}
