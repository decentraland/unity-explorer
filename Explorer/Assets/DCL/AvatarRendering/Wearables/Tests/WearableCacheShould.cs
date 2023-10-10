using DCL.AvatarRendering.Wearables.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableCacheShould
    {
        private WearableAssetsCache cache;

        [SetUp]
        public void SetUp()
        {
            cache = new WearableAssetsCache(2, 100);
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();
        }

        [Test]
        public void ReturnToPool()
        {
            var asset = new WearableAsset(new GameObject("ORIGINAL"), new List<WearableAsset.RendererInfo>());

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryRelease(new CachedWearable(asset, new WearableAssetInstance(new GameObject("INSTANCE" + i)))), Is.EqualTo(IWearableAssetsCache.ReleaseResult.ReturnedToPool));
        }

        [Test]
        public void ExceedCapacity()
        {
            var asset = new WearableAsset(new GameObject("ORIGINAL"), new List<WearableAsset.RendererInfo>());

            for (var i = 0; i < 2; i++)
                cache.TryRelease(new CachedWearable(asset, new WearableAssetInstance(new GameObject("INSTANCE" + i))));

            Assert.That(cache.TryRelease(new CachedWearable(asset, new WearableAssetInstance(new GameObject("EXCEEDING")))),
                Is.EqualTo(IWearableAssetsCache.ReleaseResult.CapacityExceeded));
        }

        [Test]
        public void GetPooledObject()
        {
            var asset = new WearableAsset(new GameObject("ORIGINAL"), new List<WearableAsset.RendererInfo>());

            for (var i = 0; i < 2; i++)
                cache.TryRelease(new CachedWearable(asset, new WearableAssetInstance(new GameObject("INSTANCE" + i))));

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryGet(asset.GameObject, out _), Is.True);
        }
    }
}
