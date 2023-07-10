using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles.Manifest.Tests
{
    [TestFixture]
    public class LoadAssetBundleManifestShould : LoadSystemBaseShould<LoadAssetBundleManifestSystem, SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreifa76qruh3g524cbpucqiex5x2wqg6aujyaus66xo7pqqpibxpu6u.json"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/bafkreid3xecd44iujaz5qekbdrt5orqdqj3wivg5zc5mya3zkorjhyrkda"}";
        private string notFoundPath => $"file://{Application.dataPath + "/../TestResources/AssetBundles/not_found.json"}";

        protected override GetAssetBundleManifestIntention CreateSuccessIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(successPath) };

        protected override void AssertSuccess(SceneAssetBundleManifest asset)
        {
            Assert.That(asset.dto.Version, Is.EqualTo("v6"));
            Assert.That(asset.convertedFiles.Count, Is.GreaterThan(0));
        }

        protected override GetAssetBundleManifestIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(notFoundPath) };

        protected override GetAssetBundleManifestIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadAssetBundleManifestSystem CreateSystem() =>
            new (world, cache, "http://www.fakepath.com/AssetBundles/", new MutexSync(), new NullBudgetProvider());
    }
}
