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


        public void Setup()
        {
            scenePartition = Substitute.For<IPartitionComponent>();
            scenePartition.Bucket.Returns(CheckColliderBoundsSystem.BUCKET_THRESHOLD);

            IPhysicsTickProvider physicsTickProvider = Substitute.For<IPhysicsTickProvider>();
            physicsTickProvider.Tick.Returns(2);

            system = new CheckColliderBoundsSystem(
                world,
                scenePartition,
                new ParcelMathHelper.SceneGeometry(Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes(-50f, 50f, -50f, 50f)),
                physicsTickProvider);

            collider = new GameObject(nameof(ColliderBoundsCheckerShould)).AddComponent<BoxCollider>();
        }


        public void CleanUp()
        {
            UnityObjectUtils.SafeDestroyGameObject(collider);
            collider = null;
        }


        public void IgnorePrimitiveCollider()
        {
            scenePartition.Bucket.Returns((byte)(CheckColliderBoundsSystem.BUCKET_THRESHOLD + 1));

            collider.center = new Vector3(-50, 0, -50);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent
            {
                Collider = collider,
            };

            world.Create(component);

            system.Update(0);

            // Still enabled
            Assert.IsTrue(collider.enabled);
        }


        public void DisableColliderOutOfBounds()
        {
            collider.center = new Vector3(-50, 0, -50);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent
            {
                Collider = collider,
            };

            world.Create(component);

            system.Update(0);

            Assert.IsFalse(collider.enabled);
        }


        public void KeepColliderWithinBounds()
        {
            collider.center = new Vector3(-20, 0, -20);
            collider.size = Vector3.one;

            var component = new PrimitiveColliderComponent
            {
                Collider = collider,
            };

            world.Create(component);

            system.Update(0);

            Assert.IsTrue(collider.enabled);
        }
    }
}
