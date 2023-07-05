using Arch.Core;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class CreateGltfAssetFromAssetBundleSystemShould : UnitySystemTestBase<CreateGltfAssetFromAssetBundleSystem>
    {
        private IGltfContainerInstantiationThrottler throttler;

        private readonly GltfContainerTestResources resources = new ();

        [SetUp]
        public void SetUp()
        {
            system = new CreateGltfAssetFromAssetBundleSystem(world, throttler = Substitute.For<IGltfContainerInstantiationThrottler>());
            throttler.Acquire(Arg.Any<int>()).Returns(true);
        }

        [TearDown]
        public void TearDown()
        {
            resources.UnloadBundle();
        }

        [Test]
        public async Task ResolveSimpleScene()
        {
            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.SIMPLE_RENDERER);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SIMPLE_RENDERER, new CancellationTokenSource()), ab);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);

            GltfContainerAsset asset = result.Asset;

            // One suitable renderer to become collider
            Assert.That(asset.VisibleColliderMeshes.Count, Is.EqualTo(1));

            // No colliders
            Assert.That(asset.InvisibleColliders.Count, Is.EqualTo(0));

            // One mesh renderer
            Assert.That(asset.Renderers.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ResolveSceneWithColliders()
        {
            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER, new CancellationTokenSource()), ab);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);

            GltfContainerAsset asset = result.Asset;

            // 196 suitable renderers to become colliders
            Assert.That(asset.VisibleColliderMeshes.Count, Is.EqualTo(196));

            // 1 Explicit collider
            Assert.That(asset.InvisibleColliders.Count, Is.EqualTo(1));

            // 196 mesh renderers
            Assert.That(asset.Renderers.Count, Is.EqualTo(196));
        }

        [Test]
        public async Task SkipIfThrottled()
        {
            throttler.Acquire(Arg.Any<int>()).Returns(false);

            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.SIMPLE_RENDERER);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SIMPLE_RENDERER, new CancellationTokenSource()), ab);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.False);
        }

        [Test]
        public void ResetThrottler()
        {
            system.BeforeUpdate(0);
            throttler.Received().Reset();
        }

        [Test]
        public async Task DoNothingIfCancelled()
        {
            throttler.Acquire(Arg.Any<int>()).Returns(false);

            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.SIMPLE_RENDERER);

            var canceledSource = new CancellationTokenSource();

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SIMPLE_RENDERER, canceledSource), ab);

            canceledSource.Cancel();

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.False);
        }

        [Test]
        public async Task ResolveExceptionIfNoGameObjects()
        {
            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.NO_GAME_OBJECTS);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.NO_GAME_OBJECTS, new CancellationTokenSource()), ab);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.TypeOf<MissingGltfAssetsException>().Or.TypeOf<EcsSystemException>().And.InnerException.TypeOf<MissingGltfAssetsException>());
        }

        [Test]
        public void PropagateAssetBundleException()
        {
            var exception = new ArgumentException();

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SIMPLE_RENDERER, new CancellationTokenSource()),
                new StreamableLoadingResult<AssetBundleData>(exception));

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.EqualTo(exception).Or.TypeOf<EcsSystemException>().And.InnerException.EqualTo(exception));
        }
    }
}
