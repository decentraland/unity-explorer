using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableCacheShould
    {
        private AttachmentsAssetsCache cache;

        [SetUp]
        public void SetUp()
        {
            cache = new AttachmentsAssetsCache(100);
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();
        }

        [Test]
        public void ReturnToPool()
        {
            var asset = new AttachmentRegularAsset(new GameObject("ORIGINAL"), new List<AttachmentRegularAsset.RendererInfo>(), null);

            for (var i = 0; i < 2; i++)
                cache.Release(new CachedAttachment(asset, new GameObject("INSTANCE" + i)));

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryGet(asset, out _), Is.True);
        }

        [Test]
        public void GetPooledObject()
        {
            var asset = new AttachmentRegularAsset(new GameObject("ORIGINAL"), new List<AttachmentRegularAsset.RendererInfo>(), null);

            for (var i = 0; i < 2; i++)
                cache.Release(new CachedAttachment(asset, new GameObject("INSTANCE" + i)));

            for (var i = 0; i < 2; i++)
                Assert.That(cache.TryGet(asset, out _), Is.True);
        }
    }
}
