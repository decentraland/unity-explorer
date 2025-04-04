using Arch.Core;
using DCL.ECSComponents;
using DCL.Time;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.Unity.SceneBoundsChecker.Tests
{
    public class ColliderBoundsCheckerShould : UnitySystemTestBase<CheckColliderBoundsSystem>
    {
        private IPartitionComponent scenePartition;
        private BoxCollider collider;
        private ParcelMathHelper.SceneGeometry sceneGeometry;
        private GameObject testRoot;

        [SetUp]
        public void Setup()
        {
            scenePartition = Substitute.For<IPartitionComponent>();
            scenePartition.Bucket.Returns(CheckColliderBoundsSystem.BUCKET_THRESHOLD);

            sceneGeometry = new ParcelMathHelper.SceneGeometry(
                Vector3.zero,
                new ParcelMathHelper.SceneCircumscribedPlanes(-50f, 50f, -50f, 50f),
                50.0f);

            IPhysicsTickProvider physicsTickProvider = Substitute.For<IPhysicsTickProvider>();
            physicsTickProvider.Tick.Returns(2);

            system = new CheckColliderBoundsSystem(
                world,
                scenePartition,
                sceneGeometry,
                physicsTickProvider);

            collider = new GameObject(nameof(ColliderBoundsCheckerShould)).AddComponent<BoxCollider>();
            testRoot = new GameObject("TestRoot");
        }

        [TearDown]
        public void CleanUp()
        {
            UnityObjectUtils.SafeDestroyGameObject(collider);
            collider = null;

            UnityObjectUtils.SafeDestroy(testRoot);
            testRoot = null;
        }

        [Test]
        public void IgnorePrimitiveCollider()
        {
            scenePartition.Bucket.Returns((byte)(CheckColliderBoundsSystem.BUCKET_THRESHOLD + 1));

            collider.transform.position = new Vector3(-50, 0, -50);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent();
            component.AssignCollider(new SDKCollider(collider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);
            component.SDKCollider.IsActiveByEntity = true;
            collider.enabled = true;
            //Simulate movement
            collider.transform.position += Vector3.one * 0.01f;

            world.Create(component);

            system.Update(0);

            // Still enabled
            Assert.IsTrue(collider.enabled);
        }

        [Test]
        public void DisableColliderOutOfBounds()
        {
            collider.transform.position = new Vector3(-50, 0, -50);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent();
            component.AssignCollider(new SDKCollider(collider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            component.SDKCollider.IsActiveByEntity = true;
            collider.enabled = true;

            //Simulate movement
            collider.transform.position += Vector3.one * 0.01f;

            world.Create(component);

            system.Update(0);

            Assert.IsFalse(collider.enabled);
        }

        [Test]
        public void KeepColliderWithinBounds()
        {
            collider.transform.position = new Vector3(-20, 0, -20);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent();
            component.AssignCollider(new SDKCollider(collider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            component.SDKCollider.IsActiveByEntity = true;
            collider.enabled = true;
            //Simulate movement
            collider.transform.position += Vector3.one * 0.01f;

            world.Create(component);

            system.Update(0);

            Assert.IsTrue(collider.enabled);
        }

        [Test]
        public void DisableColliderOutOfVerticalBounds()
        {
            collider.transform.position = new Vector3(0, 50, 0);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent();
            component.AssignCollider(new SDKCollider(collider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            component.SDKCollider.IsActiveByEntity = true;
            collider.enabled = true;
            //Simulate movement
            collider.transform.position += Vector3.one * 0.01f;

            world.Create(component);

            system.Update(0);

            Assert.IsFalse(collider.enabled);
        }

        [Test]
        public void KeepColliderWithinVerticalBounds()
        {
            collider.transform.position = new Vector3(0, 20, 0);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent();
            component.AssignCollider(new SDKCollider(collider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            component.SDKCollider.IsActiveByEntity = true;
            collider.enabled = true;
            //Simulate movement
            collider.transform.position += Vector3.one * 0.01f;

            world.Create(component);

            system.Update(0);

            Assert.IsTrue(collider.enabled);
        }

        [Test]
        public void ProcessGltfColliderOnEntityMovement()
        {
            // Create a collider GameObject
            var colliderObj = new GameObject("TestCollider");
            var boxCollider = colliderObj.AddComponent<BoxCollider>();
            boxCollider.transform.position = Vector3.zero;

            // Create an SDKCollider properly
            var sdkCollider = new SDKCollider(boxCollider);
            sdkCollider.IsActiveByEntity = true;

            // Create and set up the mock asset with the SDKCollider in a list
            var mockAssetData = Substitute.For<IStreamableRefCountData>();
            var gltfAsset = GltfContainerAsset.Create(testRoot, mockAssetData);
            gltfAsset.InvisibleColliders.Add(sdkCollider);

            // Move the collider to cause HasMoved() to return true
            boxCollider.transform.position = new Vector3(10, 10, 10);
            boxCollider.transform.hasChanged = true;

            // Create a promise and component properly
            var intent = new GetGltfContainerAssetIntention("test-src", "test-hash", new CancellationTokenSource());
            var assetPromise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world, intent, PartitionComponent.TOP_PRIORITY);

            // Need to make the promise ready with our asset
            // First, get the entity associated with the promise
            Entity promiseEntity = assetPromise.Entity;

            // Add our test result to that entity
            var result = new StreamableLoadingResult<GltfContainerAsset>(gltfAsset);
            world.Add(promiseEntity, result);

            // Create the GltfContainerComponent with State = Loading first
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone, assetPromise);
            component.State = LoadingState.Loading;

            // Create entity with the component
            var pbComponent = new PBGltfContainer { IsDirty = false };
            var entity = world.Create(component, pbComponent, PartitionComponent.TOP_PRIORITY);

            // Important: We need to retrieve the result before setting state to Finished
            // This simulates what happens in FinalizeGltfContainerLoadingSystem
            bool resultRetrieved = component.Promise.TryGetResult(world, out _);
            Assert.IsTrue(resultRetrieved, "Failed to retrieve result from promise");

            // Now we can set state to Finished
            component.State = LoadingState.Finished;
            world.Set(entity, component);

            // Update the system
            system.Update(0);

            // Check if the collider was processed - it should be active since it's within bounds
            Assert.IsTrue(gltfAsset.InvisibleColliders[0].IsActiveBySceneBounds);

            // Clean up
            UnityObjectUtils.SafeDestroy(colliderObj);
        }

        [Test]
        public void ProcessGltfColliderWhenPBComponentIsDirty()
        {
            // Create a collider GameObject
            var colliderObj = new GameObject("TestCollider");
            var boxCollider = colliderObj.AddComponent<BoxCollider>();

            // Set position and mark hasChanged as false to simulate no movement
            boxCollider.transform.position = new Vector3(10, 10, 10);
            boxCollider.transform.hasChanged = false;

            // Create an SDKCollider
            var sdkCollider = new SDKCollider(boxCollider);
            sdkCollider.IsActiveByEntity = true;

            // Create and set up the mock asset
            var mockAssetData = Substitute.For<IStreamableRefCountData>();
            var gltfAsset = GltfContainerAsset.Create(testRoot, mockAssetData);

            // Need to check if DecodedVisibleSDKColliders is null and initialize if needed
            if (gltfAsset.DecodedVisibleSDKColliders == null)
            {
                gltfAsset.DecodedVisibleSDKColliders = new List<SDKCollider>();
            }
            gltfAsset.DecodedVisibleSDKColliders.Add(sdkCollider);

            // Create a promise and component properly
            var intent = new GetGltfContainerAssetIntention("test-src", "test-hash", new CancellationTokenSource());
            var assetPromise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world, intent, PartitionComponent.TOP_PRIORITY);

            // Add our test result to the promise entity
            Entity promiseEntity = assetPromise.Entity;
            var result = new StreamableLoadingResult<GltfContainerAsset>(gltfAsset);
            world.Add(promiseEntity, result);

            // Create the GltfContainerComponent with State = Loading first
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone, assetPromise);
            component.State = LoadingState.Loading;

            // Create entity with a dirty PBComponent - this is the case we're testing
            var pbComponent = new PBGltfContainer { IsDirty = true };
            var entity = world.Create(component, pbComponent, PartitionComponent.TOP_PRIORITY);

            // Important: We need to retrieve the result before setting state to Finished
            // This simulates what happens in FinalizeGltfContainerLoadingSystem
            bool resultRetrieved = component.Promise.TryGetResult(world, out _);
            Assert.IsTrue(resultRetrieved, "Failed to retrieve result from promise");

            // Now we can set state to Finished
            component.State = LoadingState.Finished;
            world.Set(entity, component);

            // Update the system
            system.Update(0);

            // Check if the collider was processed - it should be active since PBComponent is dirty
            Assert.IsTrue(gltfAsset.DecodedVisibleSDKColliders[0].IsActiveBySceneBounds);

            // Clean up
            UnityObjectUtils.SafeDestroy(colliderObj);
        }
    }
}
