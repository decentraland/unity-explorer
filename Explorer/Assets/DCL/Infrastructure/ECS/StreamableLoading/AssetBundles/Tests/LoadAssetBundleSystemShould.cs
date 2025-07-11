using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture(WebRequestsMode.UNITY)]
    public class LoadAssetBundleSystemShould : LoadSystemBaseShould<LoadAssetBundleSystem, AssetBundleData, GetAssetBundleIntention>
    {
        public LoadAssetBundleSystemShould(WebRequestsMode webRequestsMode) : base(webRequestsMode) { }

        [TearDown]
        public void UnloadBundle()
        {
            // Unload the bundle so we load it again, otherwise it throws an exception
            if (promise.Result is { Succeeded: true })
                promise.Result.Value.Asset.AssetBundle.Unload(true);
        }

        private Uri successPath => new ($"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}");
        private Uri failPath => new ($"file://{Application.dataPath + "/../TestResources/AssetBundles/non_existing"}");
        private Uri wrongTypePath => new ($"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}");

        protected override GetAssetBundleIntention CreateSuccessIntention() =>

            // omit cacheHash so it won't be cached
            new (new CommonLoadingArguments(successPath));

        protected override GetAssetBundleIntention CreateNotFoundIntention() =>
            new (new CommonLoadingArguments(failPath));

        protected override GetAssetBundleIntention CreateWrongTypeIntention() =>
            new (new CommonLoadingArguments(wrongTypePath));

        protected override LoadAssetBundleSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, cache, webRequestController, new AssetBundleLoadingMutex());
    }
}
