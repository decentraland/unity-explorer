using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.Physics;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Asset.Tests;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using Utility;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class FinalizeGltfContainerLoadingSystemShould : UnitySystemTestBase<FinalizeGltfContainerLoadingSystem>
    {
        private readonly GltfContainerTestResources resources = new ();

        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem = null!;
        private EntityEventBuffer<GltfContainerComponent> eventBuffer = null!;

        //Required since all tests invoke TearDown, but not all used resources; therefore it could trigger a negative ref count
        private bool usedResources;

        [SetUp]
        public void SetUp()
        {
            Entity sceneRoot = world.Create(new SceneRootComponent());
            AddTransformToEntity(sceneRoot);
            IReleasablePerformanceBudget releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();
            releasablePerformanceBudget.TrySpendBudget().Returns(true);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY);

            system = new FinalizeGltfContainerLoadingSystem(
                world, sceneRoot, releasablePerformanceBudget, NullEntityCollidersSceneCache.INSTANCE, sceneData,
                eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1));

            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);
        }

        [TearDown]
        public void TearDown()
        {
            //temp try catch to circumvent false positive error due to the partial flow removal
            try {
                if (usedResources)
                    resources.UnloadBundle();

                usedResources = false;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task InstantiateAssetBundle(string hash, Entity promiseEntity)
        {
            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(hash);
            usedResources = true;
            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(promiseEntity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);
        }

        [Test]
        public void FinalizeWithError()
        {
            LogAssert.ignoreFailingMessages = true;

            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention(), PartitionComponent.TOP_PRIORITY));

            component.State = LoadingState.Loading;

            Entity e = world.Create(component, new CRDTEntity(100), new TransformComponent(), new PBGltfContainer());
            world.Add(component.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(ReportData.UNSPECIFIED, new Exception()));

            LogAssert.ignoreFailingMessages = true;

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);
            Assert.That(component.State, Is.EqualTo(LoadingState.FinishedWithError));
        }

        [Test]
        public void FinalizeWithErrorWhenAssetRootDestroyed()
        {
            LogAssert.ignoreFailingMessages = true;

            // A successfully-resolved result can reference an asset whose Root was destroyed
            // while it awaited consumption
            var asset = GltfContainerAsset.Create(new GameObject("root"), Substitute.For<IStreamableRefCountData>());
            UnityEngine.Object.DestroyImmediate(asset.Root);

            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention(), PartitionComponent.TOP_PRIORITY));

            component.State = LoadingState.Loading;

            Entity e = world.Create(component, new CRDTEntity(100), new TransformComponent(), new PBGltfContainer());
            world.Add(component.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            // Without the destroyed-Root guard this throws EcsSystemException (NRE at Root.transform)
            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);
            Assert.That(component.State, Is.EqualTo(LoadingState.FinishedWithError));
            Assert.That(component.RootGameObject, Is.Null);
            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(e, component)));

            // The consumed promise reached a terminal state: the next frame must be a no-op,
            // not an "AssetPromise is already consumed" throw
            Assert.DoesNotThrow(() => system.Update(0));
        }

        [Test]
        public async Task FinalizeWithSuccess()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            component.State = LoadingState.Loading;

            await InstantiateAssetBundle(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, component.Promise.Entity);

            Entity e = world.Create(component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH });
            TransformComponent transform = AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            Assert.That(component.State, Is.EqualTo(LoadingState.Finished));

            // Check events buffer
            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(e, component)));

            Assert.That(component.Promise.Result!.Value.Asset!.Root.transform.parent, Is.EqualTo(transform.Transform));
            Assert.That(component.Promise.Result.Value.Asset.Root.activeSelf, Is.EqualTo(true));
        }

        [Test]
        public async Task ReEnableRenderersDisabledByPreviousScene()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY))
                {
                    State = LoadingState.Loading,
                };

            await InstantiateAssetBundle(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, component.Promise.Entity);

            GltfContainerAsset asset = world.Get<StreamableLoadingResult<GltfContainerAsset>>(component.Promise.Entity).Asset!;

            asset.SetRenderersActive(false);

            Entity e = world.Create(component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH });
            AddTransformToEntity(e);

            system.Update(0);

            Assert.That(world.Get<GltfContainerComponent>(e).State, Is.EqualTo(LoadingState.Finished));
            Assert.That(asset.Renderers, Is.Not.Empty);
            Assert.That(asset.Renderers.All(r => r.enabled), Is.True);
        }

        [Test]
        public async Task InstantiateVisibleMeshesColliders()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPointer, ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            component.State = LoadingState.Loading;

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, component.Promise.Entity);

            Entity e = world.Create(component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, IsDirty = true });
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            if (component.Promise.Result is not { Asset: { DecodedVisibleSDKColliders: { } visibleColliders } })
            {
                Assert.Fail("Expected a resolved asset with decoded visible SDK colliders");
                return;
            }

            Assert.That(visibleColliders.Count, Is.EqualTo(196));
            Assert.That(visibleColliders.All(c => c.Collider?.gameObject.layer == PhysicsLayers.ON_POINTER_EVENT_LAYER), Is.True);
        }

        [Test]
        public async Task EnableInvisibleColliders()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY))
                {
                    State = LoadingState.Loading,
                };

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, component.Promise.Entity);

            Entity e = world.Create(component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH });
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            if (component.Promise.Result is not { Asset: { } promiseAsset })
            {
                Assert.Fail("Expected a resolved asset");
                return;
            }

            // 1 Collider
            Assert.That(promiseAsset.InvisibleColliders.All(c => c.IsActiveByEntity), Is.True);
            Assert.That(promiseAsset.InvisibleColliders.All(c => c.Collider?.gameObject.layer == PhysicsLayers.ON_POINTER_EVENT_LAYER), Is.True);

            // No visible colliders created
            Assert.That(promiseAsset.DecodedVisibleSDKColliders, Is.Null);
        }
    }
}
