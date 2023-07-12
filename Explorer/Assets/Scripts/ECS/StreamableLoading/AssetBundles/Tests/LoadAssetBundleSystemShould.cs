using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class LoadAssetBundleSystemShould : LoadSystemBaseShould<LoadAssetBundleSystem, AssetBundleData, GetAssetBundleIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/non_existing"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        [TearDown]
        public void UnloadBundle()
        {
            // Unload the bundle so we load it again, otherwise it throws an exception
            if (promise.Result is { Succeeded: true })
                promise.Result.Value.Asset.AssetBundle.Unload(true);
        }

        protected override GetAssetBundleIntention CreateSuccessIntention() =>

            // omit cacheHash so it won't be cached
            new () { CommonArguments = new CommonLoadingArguments(successPath) };

        protected override GetAssetBundleIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetAssetBundleIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadAssetBundleSystem CreateSystem() =>
            new (world, cache, null, new MutexSync(), new NullBudgetProvider());
    }
}
