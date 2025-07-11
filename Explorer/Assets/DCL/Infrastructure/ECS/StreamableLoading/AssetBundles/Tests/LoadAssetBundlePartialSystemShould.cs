using Arch.System;
using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ABPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture(WebRequestsMode.YET_ANOTHER)]
    public partial class LoadAssetBundlePartialSystemShould : UnitySystemTestBase<PartialLoadAssetBundleSystem>
    {
        private readonly WebRequestsMode mode;

        private const string REAL_ASSET_HASH = "bafybeidlrouln4f77ryns4wffz4pyapvfvbudno4pc2hirrhb5b554ixky";

        // 210 KB, Texture
        private static readonly string REAL_ASSET_URL =
#if UNITY_STANDALONE_WIN
            $"https://ab-cdn.decentraland.org/v38/bafkreiel5muw2s2l73uyosgizb3ko7c3zrriecxpsvc4zssk4ti454lrh4/{REAL_ASSET_HASH}_windows";
#endif

#if UNITY_STANDALONE_OSX
            $"https://ab-cdn.decentraland.org/v38/bafkreiel5muw2s2l73uyosgizb3ko7c3zrriecxpsvc4zssk4ti454lrh4/{REAL_ASSET_HASH}_mac";
#endif

        // 50KB
        private const long CHUNK_SIZE = 50 * 1024;

        private IWebRequestController webRequestController;
        private List<ABPromise> promises;

        public LoadAssetBundlePartialSystemShould(WebRequestsMode mode)
        {
            this.mode = mode;
        }

        [SetUp]
        public void Setup()
        {
            promises = new List<ABPromise>();

            webRequestController = TestWebRequestController.Create(mode, TestWebRequestController.InitializeCache(), CHUNK_SIZE);

            system = CreateSystem(webRequestController);
            system.Initialize();
        }

        [TearDown]
        public void UnloadAllBundles()
        {
            foreach (ABPromise assetPromise in promises)
                assetPromise.Consume(world);

            AssetBundle.UnloadAllAssetBundles(false);

            TestWebRequestController.RestoreCache();
        }

        /// <summary>
        ///     The first set of ABs is loaded from Embedded (not available)
        ///     The second set is loaded from Web (available)
        /// </summary>
        [Test]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(10, 5)]
        [TestCase(5, 10)]
        public async Task LoadParallelABsFromDifferentSources(int embeddedCount, int webCount)
        {
            // Create embedded and web ABs promises
            // Wait for all of them
            // There should be no exception

            promises = new List<ABPromise>(embeddedCount + webCount);

            for (var i = 0; i < webCount; i++)
                promises.Add(NewABPromise(REAL_ASSET_URL, AssetSource.WEB));

            var resolvedPromises = new ABPromise[promises.Count];

            async UniTask WaitForPromise(int index)
            {
                resolvedPromises[index] = await promises[index].ToUniTaskWithoutDestroyAsync(world);
            }

            // it will take several frames to download all chunks
            var cts = new CancellationTokenSource();

            await UniTask.WhenAny(
                UniTask.WhenAll(promises.Select((_, i) => WaitForPromise(i))).ContinueWith(() => cts.Cancel()),
                KeepUpdating(cts.Token));

            foreach (var assetPromise in resolvedPromises)
            {
                Assert.That(assetPromise.Result.HasValue, Is.True);
                Assert.That(assetPromise.Result.Value.Succeeded, Is.True);
            }
        }

        private async UniTask KeepUpdating(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                AllowAllPromisesQuery(world);

                system!.Update(0);
                await UniTask.Yield();
            }
        }

        [Query]
        private void AllowAllPromises(StreamableLoadingState loadingState)
        {
            if (loadingState.Value == StreamableLoadingState.Status.NotStarted)
                loadingState.SetAllowed(Substitute.For<IAcquiredBudget>());
        }

        [Test]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(30)]
        public async Task SharePartialStream(int promisesCount)
        {
            // No matter how many promises to the same sources are created there should be only one (the last one) to own the stream
            for (var i = 0; i < promisesCount; i++) promises.Add(NewABPromise(REAL_ASSET_URL, AssetSource.WEB));

            var resolvedPromises = new ABPromise[promises.Count];

            async UniTask WaitForPromise(int index)
            {
                resolvedPromises[index] = await promises[index].ToUniTaskWithoutDestroyAsync(world);
            }

            // it will take several frames to download all chunks
            var cts = new CancellationTokenSource();

            await UniTask.WhenAny(
                UniTask.WhenAll(promises.Select((_, i) => WaitForPromise(i))).ContinueWith(() => cts.Cancel()),
                KeepUpdating(cts.Token));

            PartialDownloadStream? stream = world.Get<StreamableLoadingState>(promises[0].Entity).PartialDownloadingData.Value.PartialDownloadStream;

            // All streams should be the same
            foreach (var assetPromise in resolvedPromises)
            {
                Assert.That(assetPromise.Result.HasValue, Is.True);
                Assert.That(assetPromise.Result.Value.Succeeded, Is.True);
                Assert.That(world.Get<StreamableLoadingState>(assetPromise.Entity).PartialDownloadingData.Value.PartialDownloadStream, Is.EqualTo(stream));
            }
        }

        private ABPromise NewABPromise(string url, AssetSource source)
        {
            var intention = GetAssetBundleIntention.FromHash(typeof(Texture), REAL_ASSET_HASH, source);
            intention.SetSources(source, source);
            intention.SetURL(URLAddress.FromString(url));
            PartitionComponent partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        private PartialLoadAssetBundleSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, new NoCache<AssetBundleData, GetAssetBundleIntention>(true, true), webRequestController, new AssetBundleLoadingMutex());
    }
}
