using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Asset.Tests;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class FinalizeGltfContainerLoadingSystemShould : UnitySystemTestBase<FinalizeGltfContainerLoadingSystem>
    {
        private readonly GltfContainerTestResources resources = new ();

        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem;

        [SetUp]
        public void SetUp()
        {
            Entity sceneRoot = world.Create();
            AddTransformToEntity(sceneRoot);
            system = new FinalizeGltfContainerLoadingSystem(world, world.Reference(sceneRoot));
            IGltfContainerInstantiationThrottler throttler = Substitute.For<IGltfContainerInstantiationThrottler>();
            throttler.Acquire(Arg.Any<int>()).Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, throttler);
        }

        [TearDown]
        public void TearDown()
        {
            resources.UnloadBundle();
        }

        private async Task InstantiateAssetBundle(string hash, Entity promiseEntity)
        {
            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(hash);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(promiseEntity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);
        }

        [Test]
        public void FinalizeWithError()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention()));

            component.State.Set(LoadingState.Loading);

            Entity e = world.Create(component, new TransformComponent(), new PBGltfContainer());
            world.Add(component.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(new Exception()));

            LogAssert.ignoreFailingMessages = true;

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);
            Assert.That(component.State.Value, Is.EqualTo(LoadingState.FinishedWithError));
        }

        [Test]
        public async Task FinalizeWithSuccess()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SIMPLE_RENDERER, new CancellationTokenSource())));

            component.State.Set(LoadingState.Loading);

            await InstantiateAssetBundle(GltfContainerTestResources.SIMPLE_RENDERER, component.Promise.Entity);

            Entity e = world.Create(component, new PBGltfContainer { Src = GltfContainerTestResources.SIMPLE_RENDERER });
            TransformComponent transform = AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            Assert.That(component.State.Value, Is.EqualTo(LoadingState.Finished));
            Assert.That(component.Promise.Result.Value.Asset.Root.transform.parent, Is.EqualTo(transform.Transform));
            Assert.That(component.Promise.Result.Value.Asset.Root.activeSelf, Is.EqualTo(true));
        }

        [Test]
        public async Task InstantiateVisibleMeshesColliders()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPointer, ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER, new CancellationTokenSource())));

            component.State.Set(LoadingState.Loading);

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER, component.Promise.Entity);

            Entity e = world.Create(component, new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER, IsDirty = true });
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);
            GltfContainerAsset promiseAsset = component.Promise.Result.Value.Asset;

            Assert.That(promiseAsset.VisibleMeshesColliders.Count, Is.EqualTo(196));
            Assert.That(promiseAsset.VisibleMeshesColliders.All(c => c.gameObject.layer == PhysicsLayers.ON_POINTER_EVENT_LAYER), Is.True);
        }

        [Test]
        public async Task EnableInvisibleColliders()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER, new CancellationTokenSource())));

            component.State.Set(LoadingState.Loading);

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER, component.Promise.Entity);

            Entity e = world.Create(component, new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER });
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            GltfContainerAsset promiseAsset = component.Promise.Result.Value.Asset;

            // 1 Collider
            Assert.That(promiseAsset.InvisibleColliders.All(c => c.enabled), Is.True);
            Assert.That(promiseAsset.InvisibleColliders.All(c => c.gameObject.layer == PhysicsLayers.ON_POINTER_EVENT_LAYER), Is.True);

            // No visible colliders created
            Assert.That(promiseAsset.VisibleMeshesColliders, Is.Null);
        }
    }
}
