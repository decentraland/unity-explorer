﻿using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using System.Buffers;
using UnityEngine;
using UnityEngine.TestTools;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class LoadAssetBundleSystemShould : LoadSystemBaseShould<LoadAssetBundleSystem, AssetBundleData, GetAssetBundleIntention>
    {
        [TearDown]
        public void UnloadBundle()
        {
            // Unload the bundle so we load it again, otherwise it throws an exception
            if (promise.Result is { Succeeded: true })
                promise.Result.Value.Asset.AssetBundle.Unload(true);
        }

        private string successPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/non_existing"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetAssetBundleIntention CreateSuccessIntention() =>

            // omit cacheHash so it won't be cached
            new (new CommonLoadingArguments(successPath));

        protected override GetAssetBundleIntention CreateNotFoundIntention() =>
            new (new CommonLoadingArguments(failPath));

        protected override GetAssetBundleIntention CreateWrongTypeIntention()
        {
            LogAssert.Expect(LogType.Error, "Failed to read data for the AssetBundle 'IO.Stream'.");
            return new GetAssetBundleIntention(new CommonLoadingArguments(wrongTypePath));
        }

        protected override LoadAssetBundleSystem CreateSystem() =>
            new (world, cache, IWebRequestController.DEFAULT, ArrayPool<byte>.Shared, new AssetBundleLoadingMutex(), Substitute.For<IDiskCache<PartialLoadingState>>());
    }
}
