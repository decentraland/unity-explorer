using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class LoadSystemBaseAbandonedResultShould
    {
        [Test]
        public async Task DisposeSuccessfulResultWhenEntityDestroyedDuringFlow()
        {
            using var mockedReportScope = new MockedReportScope();

            var world = World.Create();
            world.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

            var cache = NoCache<StubAsset, StubIntention>.INSTANCE;
            var budget = Substitute.For<IAcquiredBudget>();

            var system = new TestLoadSystem(world, cache);
            system.Initialize();

            try
            {
                var intention = new StubIntention
                {
                    CommonArguments = new CommonLoadingArguments(
                        CommunicationData.URLHelpers.URLAddress.EMPTY,
                        cancellationTokenSource: new CancellationTokenSource(),
                        attempts: 1,
                        permittedSources: AssetSource.EMBEDDED,
                        currentSource: AssetSource.EMBEDDED),
                };

                var promise = AssetPromise<StubAsset, StubIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);
                world.Get<StreamableLoadingState>(promise.Entity).SetAllowed(budget);

                // Launch the flow — FlowInternalAsync awaits our gate
                system.Update(0);

                Assume.That(system.FlowStarted, Is.True, "Flow should have started awaiting the gate");
                Assume.That(system.DisposeAbandonedCount, Is.EqualTo(0));

                // Destroy the entity BEFORE the flow completes, then let it complete with a successful result
                world.Destroy(promise.Entity);

                var asset = new StubAsset();
                system.Gate.TrySetResult(new StreamableLoadingResult<StubAsset>(asset));

                // Yield repeatedly so all continuations (SwitchToMainThread, PutAsync.Forget, finally block) run
                for (int i = 0; i < 10; i++)
                    await UniTask.Yield();

                Assert.That(system.DisposeAbandonedCount, Is.EqualTo(1),
                    "DisposeAbandonedResult must be invoked for a successful result whose owning entity died before FinalizeLoading");
                Assert.That(system.LastDisposedAsset, Is.SameAs(asset));
            }
            finally
            {
                system.Dispose();
                world.Dispose();
            }
        }

        private class StubAsset { }

        private struct StubIntention : ILoadingIntention, IEquatable<StubIntention>
        {
            public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
            public CommonLoadingArguments CommonArguments { get; set; }
            public bool Equals(StubIntention other) => true;
            public override bool Equals(object obj) => obj is StubIntention other && Equals(other);
            public override int GetHashCode() => 0;
        }

        private partial class TestLoadSystem : LoadSystemBase<StubAsset, StubIntention>
        {
            public readonly UniTaskCompletionSource<StreamableLoadingResult<StubAsset>> Gate = new ();
            public int DisposeAbandonedCount { get; private set; }
            public StubAsset? LastDisposedAsset { get; private set; }
            public bool FlowStarted { get; private set; }

            internal TestLoadSystem(World world, IStreamableCache<StubAsset, StubIntention> cache) : base(world, cache) { }

            protected override async UniTask<StreamableLoadingResult<StubAsset>> FlowInternalAsync(StubIntention intention,
                StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
            {
                FlowStarted = true;
                return await Gate.Task;
            }

            protected override void DisposeAbandonedResult(StubAsset asset)
            {
                DisposeAbandonedCount++;
                LastDisposedAsset = asset;
            }
        }
    }
}
