using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility.Types;
using ABPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class LoadAssetBundlePartialSystemShould : PartialLoadSystemBaseShould<LoadAssetBundleSystem, AssetBundleData, GetAssetBundleIntention>
    {
        //size 7600
        private string assetPath => $"{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";
        private const int REQUESTS_COUNT = 10;

        private readonly IWebRequestController webRequestController = Substitute.For<IWebRequestController>();
        private readonly IDiskCache<PartialLoadingState> diskCachePartials = Substitute.For<IDiskCache<PartialLoadingState>>();
        private readonly List<ABPromise> promises = new (REQUESTS_COUNT);

        ArrayPool<byte> BuffersPool = ArrayPool<byte>.Shared;


        [Test]
        public void ParallelABLoadsWithCacheShould()
        {
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
        public void ParallelABLoadsWithoutCacheShould()
        {
            byte[] fileBytes = File.ReadAllBytes(assetPath);
            byte[] firstChunk = new byte[5000];
            byte[] secondChunk = new byte[2600];
            Buffer.BlockCopy(fileBytes, 0, firstChunk, 0, Math.Min(5000, fileBytes.Length));
            Buffer.BlockCopy(fileBytes, 5000, secondChunk, 0, 2600);

            var intention = new GetAssetBundleIntention(new CommonLoadingArguments(assetPath));
            intention.Hash = "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda";

            //Mocking an empty result from the cache to force the webrequest controller flow
            diskCachePartials.ContentAsync(Arg.Any<HashKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(new UniTask<EnumResult<Option<PartialLoadingState>, TaskError>>(new EnumResult<Option<PartialLoadingState>, TaskError>()));

            webRequestController.GetPartialAsync(
                                     Arg.Any<CommonArguments>(),
                                     Arg.Any<CancellationToken>(),
                                     Arg.Any<ReportData>(),
                                     BuffersPool,
                                     Arg.Any<WebRequestHeadersInfo>())
                                .Returns(
                                     new UniTask<PartialDownloadedData>(new PartialDownloadedData(firstChunk, 5000, 7500)));

            for (var i = 0; i < REQUESTS_COUNT; i++) promises.Add(NewABPromise());

            //First update will download the first chunk
            system.Update(0);
            foreach (var assetPromise in promises)
            {
                StreamableLoadingState streamableLoadingState = world.Get<StreamableLoadingState>(assetPromise.Entity);
                Assert.That(streamableLoadingState.PartialDownloadingData.HasValue, Is.True);
                Assert.That(streamableLoadingState.PartialDownloadingData.Value.FullyDownloaded, Is.False);
            }

            //Second update will download the second and final chunk, marking it as fully downloaded
            system.Update(0);
            foreach (var assetPromise in promises)
            {
                StreamableLoadingState streamableLoadingState = world.Get<StreamableLoadingState>(assetPromise.Entity);
                Assert.That(streamableLoadingState.PartialDownloadingData.HasValue, Is.True);
                Assert.That(streamableLoadingState.PartialDownloadingData.Value.FullyDownloaded, Is.True);
            }
        }

        private ABPromise NewABPromise()
        {
            var intention = GetAssetBundleIntention.FromHash(typeof(GameObject), "bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda", permittedSources: AssetSource.WEB);
            var partition = PartitionComponent.TOP_PRIORITY;
            var assetPromise = ABPromise.Create(world, intention, partition);
            world.Get<StreamableLoadingState>(assetPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());
            return assetPromise;
        }

        protected override LoadAssetBundleSystem CreateSystem() =>
            new (world, cache, webRequestController, BuffersPool, new AssetBundleLoadingMutex(), diskCachePartials);

        [TearDown]
        public void Cleanup()
        {
            foreach (ABPromise assetPromise in promises)
                assetPromise.ForgetLoading(world);
        }
    }
}
