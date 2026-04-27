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
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class PrepareGltfAssetLoadingSystemShould : UnitySystemTestBase<PrepareGltfAssetLoadingSystem>
    {
        private IGltfContainerAssetsCache cache;
        private ISceneData sceneData;
        private ISceneContent sceneContent;

        [SetUp]
        public void SetUp()
        {
            cache = Substitute.For<IGltfContainerAssetsCache>();
            sceneData = Substitute.For<ISceneData>();
            sceneContent = Substitute.For<ISceneContent>();
            sceneData.SceneContent.Returns(sceneContent);
            system = new PrepareGltfAssetLoadingSystem(world, cache, sceneData, default);
        }

        [Test]
        public void CreateAssetBundleIntention()
        {
            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.TryGet(e, out GetAssetBundleIntention result), Is.True);
            Assert.That(result.Hash, Is.EqualTo($"TEST_HASH{PlatformUtils.GetCurrentPlatform()}"));
        }

        [Test]
        public void CreateGltfIntentionInLocalSceneDevelopment()
        {
            system = new PrepareGltfAssetLoadingSystem(world, cache, sceneData, new PrepareGltfAssetLoadingSystem.Options
            {
                LocalSceneDevelopment = true,
                UseRemoteAssetBundles = false,
            });

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.Has<GetAssetBundleIntention>(e), Is.False);
            Assert.That(world.Has<GetGLTFIntention>(e), Is.True);
        }

        [Test]
        public void CreateAssetBundleIntentionWhenLsdWithRemoteAb()
        {
            sceneContent.IsRawAsset("TEST").Returns(false);

            system = new PrepareGltfAssetLoadingSystem(world, cache, sceneData, new PrepareGltfAssetLoadingSystem.Options
            {
                LocalSceneDevelopment = true,
                UseRemoteAssetBundles = true,
            });

            var intent = new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.TryGet(e, out GetAssetBundleIntention result), Is.True);
            Assert.That(result.Hash, Is.EqualTo($"TEST_HASH{PlatformUtils.GetCurrentPlatform()}"));
        }

        [Test]
        public void CreateGltfIntentionForRawAssetWhenLsdWithRemoteAb()
        {
            sceneContent.IsRawAsset("models/local_only.glb").Returns(true);

            system = new PrepareGltfAssetLoadingSystem(world, cache, sceneData, new PrepareGltfAssetLoadingSystem.Options
            {
                LocalSceneDevelopment = true,
                UseRemoteAssetBundles = true,
            });

            var intent = new GetGltfContainerAssetIntention("models/local_only.glb", "TEST_HASH", new CancellationTokenSource());
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.Has<GetAssetBundleIntention>(e), Is.False);
            Assert.That(world.Has<GetGLTFIntention>(e), Is.True);
        }

        [Test]
        public void LoadFromCache()
        {
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
    }
}