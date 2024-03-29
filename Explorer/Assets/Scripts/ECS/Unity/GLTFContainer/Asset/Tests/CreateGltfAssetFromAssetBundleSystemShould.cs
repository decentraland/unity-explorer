using Arch.Core;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
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
        [SetUp]
        public void SetUp()
        {
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            system = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);
        }

        [TearDown]
        public void TearDown()
        {
            resources.UnloadBundle();
        }

        private readonly GltfContainerTestResources resources = new ();

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

            // One Animation
            Assert.That(asset.Animations.Count, Is.EqualTo(1));
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

            // No Animations
            Assert.That(asset.Animations.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task DoNothingIfCancelled()
        {
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
            Assert.That(result.Exception, Is.TypeOf<AssetBundleMissingMainAssetException>().Or.TypeOf<EcsSystemException>().And.InnerException.TypeOf<AssetBundleMissingMainAssetException>());
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
