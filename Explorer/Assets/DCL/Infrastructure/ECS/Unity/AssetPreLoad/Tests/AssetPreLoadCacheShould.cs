using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.Unity.AssetLoad.Tests
{
    [TestFixture]
    public class AssetPreLoadCacheShould
    {
        private const string KEY = "preload-key";
        private const string HASH = "preload-hash";

        private IGltfContainerAssetsCache gltfCache;
        private AssetPreLoadCache cache;
        private GameObject sourceAsset;
        private AssetBundleData assetBundleData;
        private GltfContainerAsset template;

        [SetUp]
        public void SetUp()
        {
            gltfCache = Substitute.For<IGltfContainerAssetsCache>();
            cache = new AssetPreLoadCache(gltfCache);

            // The template is built the same way the loading pipeline does: from an AssetBundleData
            // exposing a source GameObject under the asset hash
            sourceAsset = new GameObject(HASH);
            assetBundleData = new AssetBundleData(null, new Object[] { sourceAsset }, typeof(GameObject), Array.Empty<AssetBundleData>());
            assetBundleData.AcquireRef();

            Assert.That(Utils.TryCreateGltfObject(assetBundleData, HASH, out template), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            template.Dispose();
            Object.DestroyImmediate(sourceAsset);
        }

        [Test]
        public void KeepCheckedOutClonesAliveOnClear()
        {
            Assert.That(cache.TryAddGltf(KEY, HASH, template), Is.True);
            Assert.That(cache.TryGetGltfInstance(KEY, out GltfContainerAsset? clone), Is.True);

            cache.Clear();

            // The clone is owned by the container that checked it out; clearing the cache must not
            // destroy its Root under that owner
            Assert.That(clone!.Root == null, Is.False, "checked-out clone's Root must survive Clear()");

            // The template itself is released back to the assets cache
            gltfCache.Received(1).Dereference(KEY, template, false, false);
            Assert.That(cache.ContainsGltf(KEY), Is.False);

            clone.Dispose();
        }
    }
}
