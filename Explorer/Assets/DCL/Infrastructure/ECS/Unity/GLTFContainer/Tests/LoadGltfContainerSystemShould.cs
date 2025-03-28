﻿using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Asset.Tests;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.SceneBoundsChecker;
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
using System.Collections.Generic;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class LoadGltfContainerSystemShould : UnitySystemTestBase<LoadGltfContainerSystem>
    {
        private readonly GltfContainerTestResources resources = new ();

        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem;
        private EntityEventBuffer<GltfContainerComponent> eventBuffer;

        [SetUp]
        public void SetUp()
        {
            var sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetHash(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, out Arg.Any<string>())
                .Returns(x =>
                {
                    x[1] = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH;
                    return true;
                });

            sceneData.TryGetHash(GltfContainerTestResources.NO_GAME_OBJECTS, out Arg.Any<string>())
                .Returns(x =>
                {
                    x[1] = "";
                    return false;
                });
            system = new LoadGltfContainerSystem(world, eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1), sceneData, Substitute.For<IEntityCollidersSceneCache>());
            var budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);
        }

        [TearDown]
        public void TearDown()
        {
            //temp try catch to circumvent false positive error due to the partial flow removal
            try {
                resources.UnloadBundle();}
            catch (Exception e)
            {
            }
        }

        [Test]
        public void CreateGetIntent()
        {
            var sdkComponent = new PBGltfContainer
            {
                Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, InvisibleMeshesCollisionMask = (uint)(ColliderLayer.ClPhysics | ColliderLayer.ClPointer), VisibleMeshesCollisionMask = (uint)ColliderLayer.ClPointer
            };

            var entity = world.Create(sdkComponent, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            Assert.That(world.TryGet(entity, out GltfContainerComponent component), Is.True);
            Assert.That(component.Hash, Is.EqualTo(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH));
            Assert.That(component.Name, Is.EqualTo(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME));
            Assert.That(component.Promise.LoadingIntention.Hash, Is.EqualTo(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH));
            Assert.That(component.VisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClPointer));
            Assert.That(component.InvisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClPhysics | ColliderLayer.ClPointer));
            Assert.That(component.State, Is.EqualTo(LoadingState.Loading));
            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(entity, component)));
        }

        private async Task InstantiateAssetBundle(string hash, Entity promiseEntity)
        {
            var assetBundleData = await resources.LoadAssetBundle(hash);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(promiseEntity, assetBundleData, PartitionComponent.TOP_PRIORITY);
            createGltfAssetFromAssetBundleSystem.Update(0);
        }

        [Test]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task ReconfigureInvisibleColliders(bool from, bool to)
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, from ? ColliderLayer.ClPointer : ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, component.Promise.Entity);

            component.State = LoadingState.Finished;
            component.Promise.TryConsume(world, out var result);

            var e = world.Create(component, new PBGltfContainer
            {
                Src = GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH
            }, PartitionComponent.TOP_PRIORITY, new CRDTEntity());
            var transformComponent = AddTransformToEntity(e);

            ConfigureGltfContainerColliders.SetupColliders(ref component, result.Asset);

            // Reparent to the current transform
            result.Asset.Root.transform.SetParent(transformComponent.Transform);
            result.Asset.Root.transform.ResetLocalTRS();
            result.Asset.Root.SetActive(true);

            var promiseAsset = result.Asset;

            for (int i = 0; i < promiseAsset.InvisibleColliders.Count; i++)
            {
                var c = promiseAsset.InvisibleColliders[i];
                c.ForceActiveBySceneBounds(true);
                promiseAsset.InvisibleColliders[i] = c;
            }

            Assert.That(promiseAsset.InvisibleColliders.All(c => c.Collider.enabled), Is.EqualTo(from));

            // then modify the component to disable colliders

            world.Set(e, new PBGltfContainer
            {
                Src = GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, InvisibleMeshesCollisionMask = (uint)(to ? ColliderLayer.ClPointer : ColliderLayer.ClNone), IsDirty = true
            });
            system.Update(0);

            Assert.That(promiseAsset.InvisibleColliders.All(c => c.Collider.enabled), Is.EqualTo(to));
        }

        [Test]
        public void ReconfigureSource()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            var e = world.Create(component, new PBGltfContainer
            {
                Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, IsDirty = true
            }, PartitionComponent.TOP_PRIORITY, new CRDTEntity());
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            Assert.That(component.Name, Is.EqualTo(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME));
            Assert.That(component.Hash, Is.EqualTo(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH));
            Assert.That(component.State, Is.EqualTo(LoadingState.Loading));

            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(e, component)));
        }

        [Test]
        public void FailIfSceneHashIsNotAvailable()
        {
            LogAssert.ignoreFailingMessages = true;

            var e = world.Create(new PBGltfContainer
            {
                Src = GltfContainerTestResources.NO_GAME_OBJECTS, IsDirty = true
            }, PartitionComponent.TOP_PRIORITY);

            system.Update(0);

            var component = world.Get<GltfContainerComponent>(e);

            Assert.That(component.State, Is.EqualTo(LoadingState.FinishedWithError));
            Assert.That(component.Promise.LoadingIntention, Is.EqualTo(default(GetGltfContainerAssetIntention)));
            Assert.That(component.Promise.Result!.Value.Succeeded, Is.EqualTo(false));
        }

        [Test]
        public void DelayDirtyResetWhenComponentIsStillLoading()
        {
            // Set up a loading component
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER_NAME, GltfContainerTestResources.SCENE_WITH_COLLIDER_HASH, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));
            component.State = LoadingState.Loading;  // Component is still loading

            var e = world.Create(component, new PBGltfContainer
            {
                Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME, IsDirty = true
            }, PartitionComponent.TOP_PRIORITY, new CRDTEntity());

            // Update the system
            system.Update(0);

            // Get the updated PBGltfContainer
            var updatedComponent = world.Get<PBGltfContainer>(e);

            // Verify DelayDirtyReset is set to true
            Assert.That(updatedComponent.DelayDirtyReset, Is.True);
        }

        [Test]
        public void ProcessChangesThatWereDelayedAfterLoadingCompletes()
        {
            // Set up a component that will complete loading
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME,
                    GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH, new CancellationTokenSource()),
                    PartitionComponent.TOP_PRIORITY));

            // Initially the component is loading
            component.State = LoadingState.Loading;

            var sdkComponent = new PBGltfContainer
            {
                Src = GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_NAME,
                VisibleMeshesCollisionMask = (uint)ColliderLayer.ClCustom3,
                InvisibleMeshesCollisionMask = (uint)ColliderLayer.ClCustom7,
                IsDirty = true
            };

            var entity = world.Create(component, sdkComponent, PartitionComponent.TOP_PRIORITY, new CRDTEntity());

            // Update while loading - this should set DelayDirtyReset
            system.Update(0);

            // Verify DelayDirtyReset is set
            var updatedSdkComponent = world.Get<PBGltfContainer>(entity);
            Assert.That(updatedSdkComponent.DelayDirtyReset, Is.True);

            // ResetDirtyFlagSystem would reset the delay flag but not the IsDirty one
            updatedSdkComponent.DelayDirtyReset = false;
            world.Set(entity, updatedSdkComponent);

            // Now simulate the loading completing
            var updatedComponent = world.Get<GltfContainerComponent>(entity);

            // Create a mock asset properly using the Create method
            var mockRoot = new GameObject("MockRoot");
            var mockAssetData = Substitute.For<IStreamableRefCountData>();
            var mockAsset = GltfContainerAsset.Create(mockRoot, mockAssetData);

            // Create a StreamableLoadingResult with the proper asset
            var successResult = new StreamableLoadingResult<GltfContainerAsset>(mockAsset);

            // Add the successful result to the promise entity first
            Entity promiseEntity = updatedComponent.Promise.Entity;
            world.Add(promiseEntity, successResult);

            // Now we need to force Promise.Result to be populated by calling TryConsume or TryGetResult
            // We'll use TryGetResult since we still need the entity to exist
            bool resultRetrieved = updatedComponent.Promise.TryGetResult(world, out var result);
            Assert.That(resultRetrieved, Is.True, "Failed to retrieve result from promise");
            Assert.That(updatedComponent.Promise.Result.HasValue, Is.True, "Promise.Result is not set");

            // Set the state to Finished AFTER ensuring Promise.Result has a value
            updatedComponent.State = LoadingState.Finished;
            world.Set(entity, updatedComponent);

            // Update again - this should now process the changes
            system.Update(0);

            // The component should have had its collision masks updated
            updatedComponent = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedComponent.VisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClCustom3));
            Assert.That(updatedComponent.InvisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClCustom7));

            // Cleanup
            UnityObjectUtils.SafeDestroy(mockRoot);
        }
    }
}
