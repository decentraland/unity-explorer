using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.PartialDownload;
using DCL.WebRequests.RequestsHub;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Types;
using ABPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class LoadAssetBundlePartialSystemShould :  UnitySystemTestBase<LoadAssetBundleSystem>
    {
        //size 7600
        private string assetPath => $"{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";
        private const int REQUESTS_COUNT = 5;

        private readonly List<ABPromise> promises = new (REQUESTS_COUNT);
        private readonly ArrayPool<byte> buffersPool = ArrayPool<byte>.Shared;


        [Test]
        public void ParallelABLoadsWithCacheShould()
        {
            IDiskCache<PartialLoadingState> diskCachePartials = Substitute.For<IDiskCache<PartialLoadingState>>();
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
            system = CreateSystem(webRequestController, diskCachePartials);
            system.Initialize();
            byte[] fileBytes = File.ReadAllBytes(assetPath);
            var partialLoadingStateInCache = new PartialLoadingState(7600);
            partialLoadingStateInCache.AppendData(fileBytes);

            diskCachePartials.ContentAsync(Arg.Any<HashKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(new UniTask<EnumResult<Option<PartialLoadingState>, TaskError>>(EnumResult<Option<PartialLoadingState>, TaskError>.SuccessResult(Option<PartialLoadingState>.Some(partialLoadingStateInCache))));

            for (var i = 0; i < REQUESTS_COUNT; i++) promises.Add(NewABPromise());

            system.Update(0);

            foreach (var assetPromise in promises)
            {
                StreamableLoadingState streamableLoadingState = world.Get<StreamableLoadingState>(assetPromise.Entity);
                Assert.That(streamableLoadingState.PartialDownloadingData.HasValue, Is.True);
                Assert.That(streamableLoadingState.PartialDownloadingData.Value.FullyDownloaded, Is.True);
            }
        }

        [Test]
        public async Task ParallelABLoadsWithoutCacheShould()
        {
            var diskCachePartials = Substitute.For<IDiskCache<PartialLoadingState>>();
            IWebRequestController webRequestController = new WebRequestController(IWebRequestsAnalyticsContainer.DEFAULT, new IWeb3IdentityCache.Default(), new RequestHub(ITexturesFuse.NewDefault(), true));
            system = CreateSystem(webRequestController, diskCachePartials);
            system.Initialize();

            //Mocking an empty result from the cache to force the webrequest controller flow
            diskCachePartials.ContentAsync(Arg.Any<HashKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(new UniTask<EnumResult<Option<PartialLoadingState>, TaskError>>(new EnumResult<Option<PartialLoadingState>, TaskError>()));

            for (var i = 0; i < REQUESTS_COUNT; i++) promises.Add(NewABPromiseRemoteAsset(i));

            system.Update(0);

            while (!world.Get<StreamableLoadingState>(promises[0].Entity).PartialDownloadingData.HasValue)
            {
                await UniTask.Yield();
            }

            foreach (var assetPromise in promises)
            {
                StreamableLoadingState streamableLoadingState = world.Get<StreamableLoadingState>(assetPromise.Entity);
                Assert.That(streamableLoadingState.PartialDownloadingData.HasValue, Is.True);
                Assert.That(streamableLoadingState.PartialDownloadingData.Value.FullyDownloaded, Is.True);
            }
        }

        private ABPromise NewABPromiseRemoteAsset(int index)
        {
            var intention = new GetAssetBundleIntention(new CommonLoadingArguments("https://ab-cdn.decentraland.org/v36/bafkreiaetzu4kz4wqwadrlglcu5r7wyxjuvz7y2gsugtc7sqsgqv4aellu/bafkreibfutn7mfd2mu3ux6g5eg6qek3gctuhdcot2y4mjzttwzmiqrwlpi_mac"));
            intention.Hash = $"req{index}";
            var partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        private ABPromise NewABPromise()
        {
            var intention = GetAssetBundleIntention.FromHash(typeof(GameObject), "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda", permittedSources: AssetSource.WEB);
            var partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        private LoadAssetBundleSystem CreateSystem(IWebRequestController webRequestController, IDiskCache<PartialLoadingState> diskCachePartials) =>
            new (world, Substitute.For<IStreamableCache<AssetBundleData, GetAssetBundleIntention>>(), webRequestController, buffersPool, new AssetBundleLoadingMutex(), diskCachePartials);

        [TearDown]
        public void Cleanup()
        {
            foreach (ABPromise assetPromise in promises)
                assetPromise.ForgetLoading(world);
        }
    }
}
