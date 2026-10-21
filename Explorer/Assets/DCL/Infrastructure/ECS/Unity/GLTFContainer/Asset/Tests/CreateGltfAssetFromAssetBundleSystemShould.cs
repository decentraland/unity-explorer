using Arch.Core;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

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
            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, new CancellationTokenSource()), ab);

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
            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), ab);

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
            LogAssert.ignoreFailingMessages = true;

            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH);

            var canceledSource = new CancellationTokenSource();

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, canceledSource), ab);

            canceledSource.Cancel();

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.False);
        }

        [Test]
        public void FallBackToRawGltfWhenBundleFailsAndFallbackIsEnabled()
        {
            LogAssert.ignoreFailingMessages = true; // a failed loading result logs its exception on construction

            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            system = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget, rawGltfFallback: true);

            Entity e = world.Create(
                new GetGltfContainerAssetIntention("TEST", "TEST_HASH", new CancellationTokenSource()),
                GetAssetBundleIntention.FromHash("TEST_HASH", AssetBundleManifestVersion.FAILED),
                new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, new ArgumentException("bundle missing")),
                StreamableLoadingState.Create());

            system.Update(0);

            // Not failed: re-issued as a raw GLTF load on the same entity, with a fresh loading state for the GLTF lane.
            Assert.That(world.Has<StreamableLoadingResult<GltfContainerAsset>>(e), Is.False);
            Assert.That(world.Has<GetAssetBundleIntention>(e), Is.False);
            Assert.That(world.Has<StreamableLoadingResult<AssetBundleData>>(e), Is.False);
            Assert.That(world.TryGet(e, out GetGLTFIntention gltfIntention), Is.True);
            Assert.That(gltfIntention.Name, Is.EqualTo("TEST"));
            Assert.That(gltfIntention.Hash, Is.EqualTo("TEST_HASH"));
            Assert.That(world.Get<StreamableLoadingState>(e).Value, Is.EqualTo(StreamableLoadingState.Status.NotStarted));
        }

        [Test]
        [Ignore("Temporarily disabled due to incompatibility with flow changes")]
        public async Task ResolveExceptionIfNoGameObjects()
        {
            LogAssert.ignoreFailingMessages = true;

            StreamableLoadingResult<AssetBundleData> ab = await resources.LoadAssetBundle(GltfContainerTestResources.NO_GAME_OBJECTS);

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.NO_GAME_OBJECTS, GltfContainerTestResources.NO_GAME_OBJECTS, new CancellationTokenSource()), ab);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.TypeOf<AssetBundleMissingMainAssetException>().Or.TypeOf<EcsSystemException>().And.InnerException.TypeOf<AssetBundleMissingMainAssetException>());
        }

        [Test]
        [Ignore("Temporarily disabled due to incompatibility with flow changes")]
        public void PropagateAssetBundleException()
        {
            LogAssert.ignoreFailingMessages = true;

            var exception = new ArgumentException();

            Entity e = world.Create(new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, new CancellationTokenSource()),
                new StreamableLoadingResult<AssetBundleData>(ReportData.UNSPECIFIED, exception));

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<GltfContainerAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.EqualTo(exception).Or.TypeOf<EcsSystemException>().And.InnerException.EqualTo(exception));
        }
    }
}
