using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems.Load;
using DCL.Browser.DecentralandUrls;
using DCL.Diagnostics.Tests;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Web3.Identities;
using ECS;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    /// <summary>
    ///     <see cref="ECSWearablesProvider" /> is the seam between the backpack/gifting/lobby callers
    ///     and the lambdas fetch: a failed fetch (5xx/network error) must propagate as an exception, not
    ///     be converted into an empty result, so callers can distinguish "the fetch failed" from "this
    ///     wallet owns nothing". A genuine 200 with zero owned wearables must still come back as a
    ///     plain empty result.
    /// </summary>
    [TestFixture]
    public class ECSWearablesProviderShould : UnitySystemTestBase<LoadTrimmedWearablesByParamSystem>
    {
        private string emptySuccessPath => $"file://{Application.dataPath}/../TestResources/Wearables/EmptyUserParam";
        private string failPath => $"file://{Application.dataPath}/../TestResources/Wearables/non_existing";

        private MockedReportScope mockedReportScope;
        private ECSWearablesProvider provider;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = new MockedReportScope();

            IRealmData realmData = Substitute.For<IRealmData>();
            realmData.Configured.Returns(true);

            var cache = Substitute.For<IStreamableCache<TrimmedWearablesResponse, GetTrimmedWearableByParamIntention>>();

            system = new LoadTrimmedWearablesByParamSystem(world, TestWebRequestController.INSTANCE, cache, realmData,
                URLSubdirectory.FromString("Wearables"), DecentralandUrlsSource.CreateForTest(), new WearableStorage(),
                new TrimmedWearableStorage());

            system.Initialize();

            // The identity's address only ends up in the request's userID/query string, which is
            // irrelevant here because PointRequestsAt overrides the resolved URL outright.
            provider = new ECSWearablesProvider(new IWeb3IdentityCache.Fake(), world);
        }

        [TearDown]
        public void TearDown()
        {
            mockedReportScope.Dispose();
        }

        /// <summary>
        ///     Overrides the system's internal URL builder so the lambdas request resolves to a fixed
        ///     local `file://` fixture, exactly like <c>LoadWearableByParamSystemShould</c> does for the
        ///     system-level tests: every fluent call is stubbed to return the same substitute, and
        ///     <c>Build()</c>/<c>GetResult()</c> are pinned to <paramref name="path" /> regardless of the
        ///     domain/subdirectory/params the provider actually built.
        /// </summary>
        private void PointRequestsAt(string path)
        {
            IURLBuilder urlBuilder = Substitute.For<IURLBuilder>();
            urlBuilder.AppendDomainWithReplacedPath(Arg.Any<URLDomain>(), Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.AppendSubDirectory(Arg.Any<URLSubdirectory>()).Returns(urlBuilder);
            urlBuilder.GetResult().Returns(path);
            urlBuilder.Build().Returns(URLAddress.FromString(path));

            system.urlBuilder = urlBuilder;
        }

        /// <summary>
        ///     There is no budget/scheduling system running in this bare test world, so the promise
        ///     entity created inside <see cref="ECSWearablesProvider.GetTrimmedByParamsAsync" /> would sit
        ///     forever at <see cref="StreamableLoadingState.Status.NotStarted" />. This mirrors
        ///     <c>LoadSystemBaseShould.ForceAllowed</c>, but by query rather than by a directly-held
        ///     promise entity, since the provider creates and owns the promise internally.
        /// </summary>
        private void AllowPendingLoad()
        {
            var query = new QueryDescription().WithAll<StreamableLoadingState>();

            world.Query(in query, (ref StreamableLoadingState state) =>
            {
                if (state.Value == StreamableLoadingState.Status.NotStarted)
                    state.SetAllowed(Substitute.For<IAcquiredBudget>());
            });
        }

        [Test]
        public async Task ThrowOnFailedLambdasFetchInsteadOfReturningEmpty()
        {
            PointRequestsAt(failPath);

            UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> resultTask =
                provider.GetTrimmedByParamsAsync(new IWearablesProvider.Params(16, 1), CancellationToken.None);

            AllowPendingLoad();
            system.Update(0);

            Exception caught = null;

            try { await resultTask; }
            catch (Exception e) { caught = e; }

            Assert.NotNull(caught,
                "A failed lambdas fetch must propagate as an exception so callers can distinguish " +
                "'the fetch failed' from 'this wallet owns nothing' - silently returning (empty, 0) " +
                "makes a backend outage indistinguishable from an empty inventory.");
        }

        [Test]
        public async Task ReturnEmptyWithoutThrowingOnGenuineEmptySuccessResponse()
        {
            // Companion case: a real 200 with zero owned wearables must still come back as a plain
            // empty result, not be conflated with (or accidentally turned into) a failure.
            PointRequestsAt(emptySuccessPath);

            UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> resultTask =
                provider.GetTrimmedByParamsAsync(new IWearablesProvider.Params(16, 1), CancellationToken.None);

            AllowPendingLoad();
            system.Update(0);

            (IReadOnlyList<ITrimmedWearable> results, int totalAmount) result = await resultTask;

            Assert.That(result.results, Is.Empty);
            Assert.That(result.totalAmount, Is.EqualTo(0));
        }
    }
}
