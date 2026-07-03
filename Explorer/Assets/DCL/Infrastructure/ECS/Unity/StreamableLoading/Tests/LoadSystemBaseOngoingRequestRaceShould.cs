using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiles;
using DCL.WebRequests;
using DCL.WebRequests.RequestsHub;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class LoadSystemBaseOngoingRequestRaceShould
    {
        [Test]
        public async Task CheckWhenMultipleWaitersRaceOnCancellation()
        {
            // Arrange
            using var mockedReportScope = new MockedReportScope();

            var world = World.Create();
            world.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

            var cache = new TexturesCache<GetTextureIntention>();

            var mockWebRequestController = Substitute.For<IWebRequestController>();
            mockWebRequestController.RequestHub.Returns(Substitute.For<IRequestHub>());

            // Two UniTaskCompletionSources we control directly:
            //   webRequestTcsA — returned to A's FlowInternalAsync. We'll cancel it manually.
            //   webRequestTcsB — returned to B's recursive FlowInternalAsync. Never completed,
            //                    so B yields between SyncTryAdd and RemoveOngoingRequest.
            var webRequestTcsA = new UniTaskCompletionSource<Texture2D?>();
            var webRequestTcsB = new UniTaskCompletionSource<Texture2D?>();

            int sendCallCount = 0;

            // ReturnsForAnyArgs is more reliable than Returns+Arg.Any for struct arguments.
            // First call → A's pending request. Second call → B's pending request.
            mockWebRequestController
                .SendAsync<GetTextureWebRequest, GetTextureArguments, GetTextureWebRequest.CreateTextureOp, Texture2D>(
                    default, default)
                .ReturnsForAnyArgs(_ =>
                {
                    int n = Interlocked.Increment(ref sendCallCount);
                    return n == 1 ? webRequestTcsA.Task : webRequestTcsB.Task;
                });

            var system = new LoadTextureSystem(
                world, cache, mockWebRequestController,
                IDiskCache<TextureData>.Null.INSTANCE,
                Substitute.For<IProfileRepository>());

            system.Initialize();

            const string URL = "http://test/texture.png";

            var intentionA = new GetTextureIntention(URL, "", TextureWrapMode.Clamp, FilterMode.Bilinear,
                TextureType.Albedo, reportSource: "Test");

            var intentionB = new GetTextureIntention(URL, "", TextureWrapMode.Clamp, FilterMode.Bilinear,
                TextureType.Albedo, reportSource: "Test");

            var intentionC = new GetTextureIntention(URL, "", TextureWrapMode.Clamp, FilterMode.Bilinear,
                TextureType.Albedo, reportSource: "Test");

            var promiseA = AssetPromise<TextureData, GetTextureIntention>.Create(world, intentionA,
                PartitionComponent.TOP_PRIORITY);
            world.Get<StreamableLoadingState>(promiseA.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            var promiseB = AssetPromise<TextureData, GetTextureIntention>.Create(world, intentionB,
                PartitionComponent.TOP_PRIORITY);
            world.Get<StreamableLoadingState>(promiseB.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            var promiseC = AssetPromise<TextureData, GetTextureIntention>.Create(world, intentionC,
                PartitionComponent.TOP_PRIORITY);
            world.Get<StreamableLoadingState>(promiseC.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            // Act
            // system.Update(0) processes all 3 entities:
            //   A → CacheableFlowAsync → SyncTryAdd → mock SendAsync returns webRequestTcsA → YIELDS
            //   B → finds A's ongoing request → awaits source (singleContinuation)
            //   C → finds A's ongoing request → awaits source (secondaryContinuationList)
            system.Update(0);

            // Guard: verify the mock intercepted A's web request.
            // If this fails, NSubstitute isn't matching the generic SendAsync call.
            Assert.That(sendCallCount, Is.EqualTo(1),
                "Mock should have intercepted exactly 1 SendAsync call (A's web request). "
                + "If 0, the mock setup doesn't match the generic method signature.");


            webRequestTcsA.TrySetCanceled(default);

            // Verify B hit the mock for its recursive web request
            Assert.That(sendCallCount, Is.EqualTo(2),
                "B's recursive FlowAsync should have called SendAsync a second time");

            // Assert: C was cancelled
            //      When FlowAsync was recursive, C was holding the recursion guard exception
            Assert.That(world.Has<StreamableLoadingResult<TextureData>>(promiseC.Entity), Is.True,
                "Promise C should have a result from the recursion guard");

            var result = world.Get<StreamableLoadingResult<TextureData>>(promiseC.Entity);

            Assert.That(result.Succeeded, Is.False,
                "Promise C should have failed");

            Assert.That(result.Exception, Is.TypeOf<OperationCanceledException>(),
                "Promise C's exception should be OperationCanceledException");

            Assert.That(world.Has<StreamableLoadingResult<TextureData>>(promiseA.Entity), Is.False);

            Assert.That(world.Has<StreamableLoadingResult<TextureData>>(promiseB.Entity), Is.False);

            // Cleanup — system.Dispose() cancels the disposal CTS, which triggers B's chain
            system.Dispose();
            cache.Dispose();
            world.Dispose();
        }
    }
}
