using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Systems;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Asset.Tests;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Animator.Tests
{
    [TestFixture]
    public class AnimatorHandlerShould : UnitySystemTestBase<AnimatorHandlerSystem>
    {
        private Entity entity;
        private PBAnimator pbAnimator;
        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem;
        private FinalizeGltfContainerLoadingSystem finalizeGltfContainerLoadingSystem;
        private readonly GltfContainerTestResources resources = new ();

        [SetUp]
        public void SetUp()
        {
            Entity sceneRoot = world.Create(new SceneRootComponent());
            AddTransformToEntity(sceneRoot);
            IReleasablePerformanceBudget releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();
            releasablePerformanceBudget.TrySpendBudget().Returns(true);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY);
            finalizeGltfContainerLoadingSystem = new FinalizeGltfContainerLoadingSystem(world, world.Reference(sceneRoot), releasablePerformanceBudget, NullEntityCollidersSceneCache.INSTANCE, sceneData);
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);

            system = new AnimatorHandlerSystem(world);

            pbAnimator = new PBAnimator()
            {
                States =
                {
                    new PBAnimationState()
                    {
                        Clip = "bite",
                        Loop = false,
                        Playing = false,
                        Speed = 1,
                        Weight = 1,
                        ShouldReset = false
                    },
                    new PBAnimationState()
                    {
                        Clip = "swim",
                        Loop = true,
                        Playing = true,
                        Speed = 1,
                        Weight = 1,
                        ShouldReset = false
                    }
                }
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, pbAnimator);
        }

        [TearDown]
        public void TearDown()
        {
            system.Dispose();
            createGltfAssetFromAssetBundleSystem.Dispose();
            finalizeGltfContainerLoadingSystem.Dispose();
            resources.UnloadBundle();
        }

        private async Task InitializeGlftContainerComponent()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.ANIMATION, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            component.State.Set(LoadingState.Loading);

            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(GltfContainerTestResources.ANIMATION);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(component.Promise.Entity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);

            world.Add(entity, component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.ANIMATION });

            finalizeGltfContainerLoadingSystem.Update(0);
        }

        [Test]
        public void LoadedAnimationsShould()
        {
            var world = World.Create();
            var system = new AnimatorHandlerSystem(world);

            var pbAnimator = new PBAnimator();
            var gltf = new GltfContainerComponent();
            gltf.State.Set(LoadingState.Finished);

            var gm = new GameObject();
            var container = GltfContainerAsset.Create(new GameObject(), null!);
            var animation = gm.AddComponent<Animation>();

            var loadingResult = new StreamableLoadingResult<GltfContainerAsset>(container);

            gltf.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.CreateFinalized(
                new GetGltfContainerAssetIntention(),
                loadingResult
            );

            loadingResult.Asset!.Animations.Add(animation);

            var entity = world.Create(pbAnimator, gltf);

            system.Update(0);

            Assert.IsTrue(world.Has<LoadedAnimationsComponent>(entity));
        }
    }
}
