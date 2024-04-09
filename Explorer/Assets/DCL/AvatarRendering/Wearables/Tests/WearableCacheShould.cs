using DCL.AvatarRendering.Wearables.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableCacheShould
    {
        private WearableAssetsCache cache;


        public void SetUp()
        {
            cache = new WearableAssetsCache(100);
        }


        public void TearDown()
        {
            cache.Dispose();
        }


        public void ReturnToPool()
        {
            var asset = new WearableRegularAsset(new GameObject("ORIGINAL"), new List<WearableRegularAsset.RendererInfo>(), null);

            for (var i = 0; i < 2; i++)
                cache.Release(new CachedWearable(asset, new GameObject("INSTANCE" + i)));

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryGet(asset, out _), Is.True);
        }


        public void GetPooledObject()
        {
            var asset = new WearableRegularAsset(new GameObject("ORIGINAL"), new List<WearableRegularAsset.RendererInfo>(), null);

            for (var i = 0; i < 2; i++)
                cache.Release(new CachedWearable(asset, new GameObject("INSTANCE" + i)));

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryGet(asset, out _), Is.True);
        }
    }
}
