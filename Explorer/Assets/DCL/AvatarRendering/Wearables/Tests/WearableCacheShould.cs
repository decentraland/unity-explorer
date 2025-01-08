using DCL.AvatarRendering.Loading.Assets;
using DCL.Optimization.Pools;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableCacheShould
    {
        private AttachmentsAssetsCache cache;
        private IComponentPoolsRegistry poolsRegistry;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            cache = new AttachmentsAssetsCache(100, poolsRegistry);
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
