﻿using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
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
using UnityEngine.TestTools;
using Utility;

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
    }
}
