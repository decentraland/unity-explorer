using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles.Tests
{

    public class LoadAssetBundleSystemShould : LoadSystemBaseShould<LoadAssetBundleSystem, AssetBundleData, GetAssetBundleIntention>
    {

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

        protected override GetAssetBundleIntention CreateWrongTypeIntention() =>
            new (new CommonLoadingArguments(wrongTypePath));

        protected override LoadAssetBundleSystem CreateSystem() =>
            new (world, cache, new MutexSync(), new AssetBundleLoadingMutex());
    }
}
