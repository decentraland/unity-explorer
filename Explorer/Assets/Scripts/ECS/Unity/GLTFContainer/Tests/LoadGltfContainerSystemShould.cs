﻿using Arch.Core;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class LoadGltfContainerSystemShould : UnitySystemTestBase<LoadGltfContainerSystem>
    {
        private readonly GltfContainerTestResources resources = new ();

        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem;

        [SetUp]
        public void SetUp()
        {
            system = new LoadGltfContainerSystem(world);
            IGltfContainerInstantiationThrottler throttler = Substitute.For<IGltfContainerInstantiationThrottler>();
            throttler.Acquire(Arg.Any<int>()).Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, throttler);
        }

        [TearDown]
        public void TearDown()
        {
            resources.UnloadBundle();
        }

        [Test]
        public void CreateGetIntent()
        {
            var sdkComponent = new PBGltfContainer
            {
                Src = GltfContainerTestResources.SIMPLE_RENDERER,
                InvisibleMeshesCollisionMask = (uint)(ColliderLayer.ClPhysics | ColliderLayer.ClPointer),
                VisibleMeshesCollisionMask = (uint)ColliderLayer.ClPointer,
            };

            Entity entity = world.Create(sdkComponent);

            system.Update(0);

            Assert.That(world.TryGet(entity, out GltfContainerComponent component), Is.True);
            Assert.That(component.Source, Is.EqualTo(GltfContainerTestResources.SIMPLE_RENDERER));
            Assert.That(component.Promise.LoadingIntention.Name, Is.EqualTo(GltfContainerTestResources.SIMPLE_RENDERER));
            Assert.That(component.VisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClPointer));
            Assert.That(component.InvisibleMeshesCollisionMask, Is.EqualTo(ColliderLayer.ClPhysics | ColliderLayer.ClPointer));
            Assert.That(component.State.Value, Is.EqualTo(LoadingState.Loading));
            Assert.That(component.State.ChangedThisFrame(), Is.True);
        }

        private async Task InstantiateAssetBundle(string hash, Entity promiseEntity)
        {
            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(hash);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(promiseEntity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);
        }

        [Test]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task ReconfigureInvisibleColliders(bool from, bool to)
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, from ? ColliderLayer.ClPointer : ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER, new CancellationTokenSource())));

            await InstantiateAssetBundle(GltfContainerTestResources.SCENE_WITH_COLLIDER, component.Promise.Entity);

            component.State.Set(LoadingState.Finished);
            component.Promise.TryConsume(world, out StreamableLoadingResult<GltfContainerAsset> result);

            Entity e = world.Create(component, new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER });
            TransformComponent transformComponent = AddTransformToEntity(e);

            ConfigureGltfContainerColliders.SetupColliders(ref component, result.Asset);

            // Reparent to the current transform
            result.Asset.Root.transform.SetParent(transformComponent.Transform);
            result.Asset.Root.transform.ResetLocalTRS();
            result.Asset.Root.SetActive(true);

            GltfContainerAsset promiseAsset = result.Asset;
            Assert.That(promiseAsset.InvisibleColliders.All(c => c.enabled), Is.EqualTo(from));

            // then modify the component to disable colliders

            world.Set(e, new PBGltfContainer { Src = GltfContainerTestResources.SCENE_WITH_COLLIDER, InvisibleMeshesCollisionMask = (uint)(to ? ColliderLayer.ClPointer : ColliderLayer.ClNone), IsDirty = true });
            system.Update(0);

            Assert.That(promiseAsset.InvisibleColliders.All(c => c.enabled), Is.EqualTo(to));
        }

        [Test]
        public void ReconfigureSource()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.SCENE_WITH_COLLIDER, new CancellationTokenSource())));

            Entity e = world.Create(component, new PBGltfContainer { Src = GltfContainerTestResources.SIMPLE_RENDERER, IsDirty = true });
            AddTransformToEntity(e);

            system.Update(0);

            component = world.Get<GltfContainerComponent>(e);

            Assert.That(component.Source, Is.EqualTo(GltfContainerTestResources.SIMPLE_RENDERER));
            Assert.That(component.State.Value, Is.EqualTo(LoadingState.Loading));
        }
    }
}
