using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Systems;
using ECS.Abstract;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Animator.Tests
{
    [TestFixture]
    public class AnimatorHandlerShould : UnitySystemTestBase<LegacyAnimationPlayerSystem>
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
            finalizeGltfContainerLoadingSystem = new FinalizeGltfContainerLoadingSystem(world, world.Reference(sceneRoot), releasablePerformanceBudget,
                NullEntityCollidersSceneCache.INSTANCE, sceneData, new EntityEventBuffer<GltfContainerComponent>(1));
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);

            system = new LegacyAnimationPlayerSystem(world);

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

            component.State = LoadingState.Loading;

            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(GltfContainerTestResources.ANIMATION);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(component.Promise.Entity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);

            world.Add(entity, component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.ANIMATION });

            finalizeGltfContainerLoadingSystem.Update(0);
        }


        [Test]
        public async Task AddAnimatorComponentToEntityWithPBAnimator()
        {
            await InitializeGlftContainerComponent();

            world.Set(entity, pbAnimator);
            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<SDKAnimatorComponent>().WithAll<PBAnimator>()));
        }

        [Test]
        public async Task UpdateAnimationStatesOnAnimatorComponentDirty()
        {
            await InitializeGlftContainerComponent();

            world.Set(entity, pbAnimator);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref SDKAnimatorComponent comp, ref GltfContainerComponent containerComponent) =>
                Assert.IsTrue(comp.SDKAnimationStates.First().Speed.Equals(1) &&
                              containerComponent.Promise.Result.Value.Asset.Animations.First()[comp.SDKAnimationStates.First().Clip].speed == 1));


            pbAnimator.States.First().Speed = 5;
            pbAnimator.IsDirty = true;

            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref SDKAnimatorComponent comp, ref GltfContainerComponent containerComponent) =>
                Assert.IsTrue(comp.SDKAnimationStates.First().Speed.Equals(5) &&
                              containerComponent.Promise.Result.Value.Asset.Animations.First()[comp.SDKAnimationStates.First().Clip].speed == 5));
        }


        [Test]
        public async Task ResetAnimationOnPBAnimatorRemoved()
        {
            await InitializeGlftContainerComponent();
            pbAnimator.States.First().Loop = false;

            world.Set(entity, pbAnimator);
            system.Update(0);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref SDKAnimatorComponent comp, ref GltfContainerComponent containerComponent) =>
                Assert.IsTrue(comp.SDKAnimationStates.First().Loop == false &&
                              containerComponent.Promise.Result.Value.Asset.Animations.First()[comp.SDKAnimationStates.First().Clip].wrapMode == WrapMode.Default));

            world.Remove<PBAnimator>(entity);

            system.HandleComponentRemovalQuery(world);

            world.Query(new QueryDescription().WithAll<PBTween>(), (ref SDKAnimatorComponent comp, ref GltfContainerComponent containerComponent) =>
                Assert.IsTrue(comp.SDKAnimationStates.First().Loop == false &&
                              containerComponent.Promise.Result.Value.Asset.Animations.First()[comp.SDKAnimationStates.First().Clip].wrapMode == WrapMode.Loop));

        }


    }
}
