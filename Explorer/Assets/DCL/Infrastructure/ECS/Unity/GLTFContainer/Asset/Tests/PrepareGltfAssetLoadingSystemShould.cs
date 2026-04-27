using Arch.Core;
using DCL.Utility;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class PrepareGltfAssetLoadingSystemShould : UnitySystemTestBase<PrepareGltfAssetLoadingSystem>
    {
        [SetUp]
        public void SetUp()
        {
            cache = Substitute.For<IGltfContainerAssetsCache>();
        }

        private IGltfContainerAssetsCache cache;

        private void BuildSystem(PrepareGltfAssetLoadingSystem.Options options = default)
        {
            system = new PrepareGltfAssetLoadingSystem(world, cache, options);
        }

        [Test]
        public void CreateAssetBundleIntention()
        {
            BuildSystem();

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.TryGet(e, out GetAssetBundleIntention result), Is.True);
            Assert.That(result.Hash, Is.EqualTo($"TEST_HASH{PlatformUtils.GetCurrentPlatform()}"));
        }

        [Test]
        public void LoadFromCache()
        {
            BuildSystem();

            var asset = GltfContainerAsset.Create(new GameObject("GLTF_ROOT"), assetData: null);

            cache.TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>())
                 .Returns(c =>
                  {
                      c[1] = asset;
                      return true;
                  });

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            cache.Received(1).TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>());
            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Asset, Is.EqualTo(asset));
        }

        [Test]
        public void LoadFromCacheInLocalSceneDevelopment()
        {
            // LSD must hit the GltfContainerAssetsCache like any other path — per-consumer cloning in
            // CreateGltfAssetFromRawGltfSystem makes cache reuse safe across multiple entities.
            BuildSystem(new PrepareGltfAssetLoadingSystem.Options { LocalSceneDevelopment = true, UseRemoveAssetBundles = false });

            var asset = GltfContainerAsset.Create(new GameObject("GLTF_ROOT"), assetData: null);

            cache.TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>())
                 .Returns(c =>
                  {
                      c[1] = asset;
                      return true;
                  });

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            cache.Received(1).TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>());
            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Asset, Is.EqualTo(asset));
            Assert.That(world.Has<GetGLTFIntention>(e), Is.False,
                "Cache hit should short-circuit — no raw GLTF load should be triggered");
        }

        [Test]
        public void FallBackToRawGltfLoadOnCacheMissInLocalSceneDevelopment()
        {
            BuildSystem(new PrepareGltfAssetLoadingSystem.Options { LocalSceneDevelopment = true, UseRemoveAssetBundles = false });

            // Cache miss: default Substitute returns false
            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            cache.Received(1).TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>());
            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.Has<GetGLTFIntention>(e), Is.True,
                "LSD cache miss must still fall through to the raw GLTF load path");
        }

        [Test]
        public void BypassCacheInBuilderPreview()
        {
            // Builder preview authors iterate collections and need the latest content on every load;
            // bypassing the cache keeps that workflow correct.
            BuildSystem(new PrepareGltfAssetLoadingSystem.Options { PreviewingBuilderCollection = true });

            var asset = GltfContainerAsset.Create(new GameObject("GLTF_ROOT"), assetData: null);

            cache.TryGet("TEST_HASH", out Arg.Any<GltfContainerAsset>())
                 .Returns(c =>
                  {
                      c[1] = asset;
                      return true;
                  });

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            cache.DidNotReceive().TryGet(Arg.Any<string>(), out Arg.Any<GltfContainerAsset>());
            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.Has<GetGLTFIntention>(e), Is.True);
        }
    }
}
