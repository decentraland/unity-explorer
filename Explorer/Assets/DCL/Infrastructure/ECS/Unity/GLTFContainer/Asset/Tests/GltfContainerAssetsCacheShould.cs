using DCL.Optimization.Pools;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class GltfContainerAssetsCacheShould
    {
        private GltfContainerAssetsCache cache;

        [SetUp]
        public void SetUp()
        {
            cache = new GltfContainerAssetsCache(Substitute.For<IComponentPoolsRegistry>());
        }

        [Test]
        public void PoolLiveAssetOnDereference()
        {
            var asset = GltfContainerAsset.Create(new GameObject(), assetData: null);

            cache.Dereference("hash", asset);

            Assert.That(cache.TryGet("hash", out GltfContainerAsset? pooled), Is.True);
            Assert.That(pooled, Is.SameAs(asset));

            Object.DestroyImmediate(asset.Root);
        }

        [Test]
        public void SkipDereferenceWhenRootAlreadyDestroyed()
        {
            // A stale promise result can reference an asset whose Root was destroyed by a prior Unload.
            var asset = GltfContainerAsset.Create(new GameObject(), assetData: null);
            Object.DestroyImmediate(asset.Root);

            Assert.DoesNotThrow(() => cache.Dereference("hash", asset));

            // The dead asset must not be re-pooled, otherwise TryGet would hand a destroyed instance back out.
            Assert.That(cache.TryGet("hash", out _), Is.False);
        }
    }
}
