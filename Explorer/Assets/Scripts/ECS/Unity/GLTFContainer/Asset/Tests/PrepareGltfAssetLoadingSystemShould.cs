using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class PrepareGltfAssetLoadingSystemShould : UnitySystemTestBase<PrepareGltfAssetLoadingSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new PrepareGltfAssetLoadingSystem(world, cache = Substitute.For<IGltfContainerAssetsCache>(), "0,0");
        }

        private IGltfContainerAssetsCache cache;

        [Test]
        public void CreateAssetBundleIntention()
        {
            var intent = new GetGltfContainerAssetIntention("TEST", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.TryGet(e, out GetAssetBundleIntention result), Is.True);
            Assert.That(result.Name, Is.EqualTo("TEST"));
        }

        [Test]
        public void LoadFromCache()
        {
            var asset = GltfContainerAsset.Create(new GameObject("GLTF_ROOT"), null);

            cache.TryGet("0,0", "TEST", out Arg.Any<GltfContainerAsset>())
                 .Returns(c =>
                  {
                    c[2] = asset;
                      return true;
                  });

            var intent = new GetGltfContainerAssetIntention("TEST", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            cache.Received(1).TryGet("0,0", "TEST", out Arg.Any<GltfContainerAsset>());
            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Asset, Is.EqualTo(asset));
        }
    }
}
