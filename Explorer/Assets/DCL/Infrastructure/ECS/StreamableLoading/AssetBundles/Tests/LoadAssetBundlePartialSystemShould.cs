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
using System.Threading.Tasks;
using UnityEngine;
using ABPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class LoadAssetBundlePartialSystemShould : UnitySystemTestBase<PartialLoadAssetBundleSystem>
    {
        //size 64800
        private static readonly string LOCAL_ASSET_PATH = $"{Application.dataPath + "/../TestResources/AssetBundles/shark"}";

        private const string REAL_ASSET_HASH = "bafybeigyvd42xyc3uh4n2dbol26jigyynbpewvhpnq6tc355y3kqc5hfra";

        // 1.7 MB
        private static readonly string REAL_ASSET_URL =
#if UNITY_STANDALONE_WIN
            $"https://ab-cdn.decentraland.org/v36/bafkreia5mfpavvkbqm7qw6by7uzjodqq5nfuf5t2b5b46cquq54cunihxu/{REAL_ASSET_HASH}_windows";
#endif

#if UNITY_STANDALONE_OSX
            $"https://ab-cdn.decentraland.org/v36/bafkreia5mfpavvkbqm7qw6by7uzjodqq5nfuf5t2b5b46cquq54cunihxu/{REAL_ASSET_HASH}_mac";
#endif

        private static readonly string NOT_EXISTENT_EMBEDDED_URL =
            $"file://{Application.dataPath + $"/../TestResources/AssetBundles/{REAL_ASSET_HASH}"}";

        // 512KB
        private const long CHUNK_SIZE = 512 * 1024;

        private IWebRequestController webRequestController;
        private List<ABPromise> promises;

        [SetUp]
        public void Setup()
        {
            promises = new List<ABPromise>();

            webRequestController = TestWebRequestController.Create(WebRequestsMode.HTTP2,
                TestWebRequestController.InitializeCache(), CHUNK_SIZE);

            system = CreateSystem(webRequestController);
            system.Initialize();
        }

        [TearDown]
        public void RestoreCache()
        {
            TestWebRequestController.RestoreCache();
        }

        [TearDown]
        public void UnloadAllBundles()
        {
            foreach (ABPromise assetPromise in promises)
                assetPromise.Consume(world);

            AssetBundle.UnloadAllAssetBundles(false);
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

            for (var i = 0; i < embeddedCount; i++)
                promises.Add(NewABPromise(NOT_EXISTENT_EMBEDDED_URL, AssetSource.EMBEDDED));

            for (var i = 0; i < webCount; i++)
                promises.Add(NewABPromise(REAL_ASSET_URL, AssetSource.WEB));

            system!.Update(0);

            var resolvedPromises = new ABPromise[promises.Count];

            async UniTask WaitForPromise(int index)
            {
                resolvedPromises[index] = await promises[index].ToUniTaskWithoutDestroyAsync(world);
            }

            await UniTask.WhenAll(promises.Select((_, i) => WaitForPromise(i)));

            foreach (var assetPromise in resolvedPromises)
            {
                Assert.That(assetPromise.Result.HasValue, Is.True);
                Assert.That(assetPromise.Result.Value.Succeeded, Is.True);
            }
        }

        [Test]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(30)]
        public async Task SharePartialStream(int promisesCount)
        {
            // No matter how many promises to the same sources are created there should be only one (the last one) to own the stream
            for (var i = 0; i < promisesCount; i++) promises.Add(NewABPromise());

            system!.Update(0);

            var resolvedPromises = new ABPromise[promises.Count];

            async UniTask WaitForPromise(int index)
            {
                resolvedPromises[index] = await promises[index].ToUniTaskWithoutDestroyAsync(world);
            }

            await UniTask.WhenAll(promises.Select((_, i) => WaitForPromise(i)));

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
            var intention = GetAssetBundleIntention.FromHash(typeof(GameObject), REAL_ASSET_HASH, source);
            intention.SetSources(source, source);
            intention.SetURL(URLAddress.FromString(url));
            PartitionComponent partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        private ABPromise NewABPromise()
        {
            var intention = GetAssetBundleIntention.FromHash(typeof(GameObject), "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda", permittedSources: AssetSource.WEB);
            PartitionComponent? partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        private PartialLoadAssetBundleSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, new NoCache<AssetBundleData, GetAssetBundleIntention>(true, false), webRequestController, new AssetBundleLoadingMutex());
    }
}
