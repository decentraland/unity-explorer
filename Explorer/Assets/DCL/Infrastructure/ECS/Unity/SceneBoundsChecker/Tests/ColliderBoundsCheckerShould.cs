using DCL.ECSComponents;
using DCL.Time;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.PrimitiveColliders.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace ECS.Unity.SceneBoundsChecker.Tests
{
    public class ColliderBoundsCheckerShould : UnitySystemTestBase<CheckColliderBoundsSystem>
    {
        private IPartitionComponent scenePartition;
        private BoxCollider collider;

        [SetUp]
        public void Setup()
        {
            scenePartition = Substitute.For<IPartitionComponent>();
            scenePartition.Bucket.Returns(CheckColliderBoundsSystem.BUCKET_THRESHOLD);

            IPhysicsTickProvider physicsTickProvider = Substitute.For<IPhysicsTickProvider>();
            physicsTickProvider.Tick.Returns(2);

            system = new CheckColliderBoundsSystem(
                world,
                scenePartition,
                new ParcelMathHelper.SceneGeometry(Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes(-50f, 50f, -50f, 50f), 50.0f),
                physicsTickProvider);

            collider = new GameObject(nameof(ColliderBoundsCheckerShould)).AddComponent<BoxCollider>();
        }

        [TearDown]
        public void CleanUp()
        {
            UnityObjectUtils.SafeDestroyGameObject(collider);
            collider = null;
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
    }
}
