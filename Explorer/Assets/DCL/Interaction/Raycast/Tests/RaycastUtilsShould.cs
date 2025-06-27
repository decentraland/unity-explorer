using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Systems;
using DCL.Interaction.Utility;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.Raycast.Tests
{
    public class RaycastUtilsShould : UnitySystemTestBase<ExecuteRaycastSystem>
    {
        private readonly List<Collider> temp = new ();
        private TransformComponent ts;

        [SetUp]
        public void Setup()
        {
            Entity e = world.Create(new PBRaycast());
            ts = AddTransformToEntity(e);
        }

        [TearDown]
        public void DestroyGarbage()
        {
            foreach (Collider o in temp)
                UnityObjectUtils.SafeDestroyGameObject(o);

            temp.Clear();
        }

        [Test]
        public void CreateLocalDirectionRay()
        {
            var rot = Quaternion.Euler(0, 45, 0);
            ts.SetTransform(new Vector3(2, 1, 0), rot, Vector3.one);

            var pbRaycast = new PBRaycast { LocalDirection = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 10 } };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(2, 1, 0)));
            Assert.That(ray.direction, Is.EqualTo(rot * new Vector3(0, 0, 10).normalized));
        }

        [Test]
        public void CreateGlobalTargetRay()
        {
            var pbRaycast = new PBRaycast
            {
                GlobalTarget = new Decentraland.Common.Vector3 { X = 10, Y = 5, Z = 0 },
                OriginOffset = new Decentraland.Common.Vector3 { X = 2, Y = 0, Z = 0 },
            };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), new Vector3(0, 0, 10), in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(10, 5, 10).normalized));
        }

        [Test]
        public void CreateTargetEntityRay()
        {
            Entity targetEntity = world.Create();
            TransformComponent targetTs = AddTransformToEntity(targetEntity);

            targetTs.SetTransform(new Vector3(100, 0, 300), Quaternion.identity, Vector3.one);
            world.Set(targetEntity, targetTs);
            ts.SetTransform(new Vector3(-100, 500, 1000), Quaternion.identity, Vector3.one);

            IReadOnlyDictionary<CRDTEntity, Entity> dict = Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>();

            dict.TryGetValue(115, out Arg.Any<Entity>())
                .Returns(x =>
                 {
                     x[1] = targetEntity;
                     return true;
                 });

            var pbRaycast = new PBRaycast { TargetEntity = 115 };

            Assert.That(pbRaycast.TryCreateRay(world, dict, Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(-100, 500, 1000)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(200, -500, -700).normalized));
        }

        [Test]
        public void CreateGlobalDirectionRay()
        {
            var pbRaycast = new PBRaycast { GlobalDirection = new Decentraland.Common.Vector3 { X = -5, Y = 0, Z = 10 } };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(0, 0, 0)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(-5, 0, 10).normalized));
        }

        [Test]
        public void FillSDKRaycastHit()
        {
            BoxCollider collider = new GameObject(nameof(RaycastUtilsShould)).AddComponent<BoxCollider>();
            collider.name = "custom";
            collider.isTrigger = true;
            collider.center = new Vector3(0, 0, 10);
            collider.size = Vector3.one;

            temp.Add(collider);

            var ray = new Ray(Vector3.zero, Vector3.forward);
            Physics.Raycast(ray, out RaycastHit hit, 100, ~0, QueryTriggerInteraction.Collide);

            var sdkHit = new ECSComponents.RaycastHit { Direction = new Decentraland.Common.Vector3(), GlobalOrigin = new Decentraland.Common.Vector3(), Position = new Decentraland.Common.Vector3(), NormalHit = new Decentraland.Common.Vector3() };

            var sceneRoot = new GameObject("SceneRoot");
            sceneRoot.transform.position = new Vector3(1, 0, 1);

            sdkHit.FillSDKRaycastHit(sceneRoot.transform, hit, collider.name, 100, Vector3.zero, Vector3.forward);

            Assert.That(sdkHit.EntityId, Is.EqualTo(100u));
            Assert.That(sdkHit.MeshName, Is.EqualTo("custom"));
            Assert.That(sdkHit.Length, Is.EqualTo(9.5f).Within(0.01f));
            Assert.That((Vector3)sdkHit.NormalHit, Is.EqualTo(hit.normal));
            Assert.That((Vector3)sdkHit.Position, Is.EqualTo(hit.point - new Vector3(1, 0, 1)));
            Assert.That((Vector3)sdkHit.GlobalOrigin, Is.EqualTo(Vector3.zero));
            Assert.That((Vector3)sdkHit.Direction, Is.EqualTo(Vector3.forward));

            UnityObjectUtils.SafeDestroyGameObject(sceneRoot.transform);
        }

        [Test]
        public void CreateRayFromDynamicallyUpdatedPosition()
        {
            // Arrange: Set an initial transform state
            var initialPosition = new Vector3(5, 5, 5);
            var initialRotation = Quaternion.Euler(0, 90, 0);
            ts.SetTransform(initialPosition, initialRotation, Vector3.one);

            // Simulate a direct update to the Unity Transform's position and rotation after initial caching
            // This is the scenario where Cached.WorldPosition/Rotation could be stale
            var updatedPosition = new Vector3(50, 50, 50);
            ts.Transform.position = updatedPosition;

            var updatedRotation = Quaternion.Euler(0, 180, 0); // New rotation
            ts.Transform.rotation = updatedRotation;
            // Note: ts.Transform.lossyScale remains Vector3.one

            var originOffset = new Decentraland.Common.Vector3 { X = 1, Y = 2, Z = 3 };
            var localDirectionVec = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 1 }; // Forward relative to entity

            var pbRaycast = new PBRaycast
            {
                OriginOffset = originOffset,
                LocalDirection = localDirectionVec
            };

            // Act: Create the ray
            bool success = pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), Vector3.zero, in ts, out Ray ray);

            // Assert
            Assert.That(success, Is.True);

            // The ray origin should be based on the *updated* Transform.position + offset
            var expectedOrigin = updatedPosition + new Vector3(originOffset.X, originOffset.Y, originOffset.Z);
            Assert.That(ray.origin, Is.EqualTo(expectedOrigin));

            // The ray direction should respect the *updated* rotation
            var expectedDirection = updatedRotation * new Vector3(localDirectionVec.X, localDirectionVec.Y, localDirectionVec.Z).normalized;
            Assert.That(ray.direction, Is.EqualTo(expectedDirection));
        }

        [Test]
        public void CreateTargetEntityRayWithDynamicUpdates()
        {
            // Arrange: Setup source and target entities
            CRDTEntity sourceCrdtEntity = new CRDTEntity(10);
            Entity sourceEntity = world.Create(new PBRaycast()); // PBRaycast is on source
            TransformComponent sourceTs = AddTransformToEntity(sourceEntity);
            world.Set(sourceEntity, sourceTs); // Ensure transform is in world

            CRDTEntity targetCrdtEntity = new CRDTEntity(20);
            Entity targetEntity = world.Create();
            TransformComponent targetTs = AddTransformToEntity(targetEntity);
            world.Set(targetEntity, targetTs); // Ensure transform is in world

            // Initial positions and rotations (cached via SetTransform)
            var initialSourcePos = new Vector3(1, 1, 1);
            sourceTs.SetTransform(initialSourcePos, Quaternion.identity, Vector3.one);

            var initialTargetPos = new Vector3(10, 1, 1);
            targetTs.SetTransform(initialTargetPos, Quaternion.identity, Vector3.one);

            // Dynamically update positions after SetTransform (simulating stale cache)
            var updatedSourcePos = new Vector3(5, 5, 5);
            sourceTs.Transform.position = updatedSourcePos;

            var updatedTargetPos = new Vector3(15, 5, 5);
            targetTs.Transform.position = updatedTargetPos;

            // Setup entity map
            var entitiesMap = Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>();
            entitiesMap.TryGetValue(targetCrdtEntity, out Arg.Any<Entity>())
                       .Returns(x =>
                        {
                            x[1] = targetEntity;
                            return true;
                        });
            entitiesMap.TryGetValue(sourceCrdtEntity, out Arg.Any<Entity>()) // Not strictly needed for this raycast type, but good for completeness
                       .Returns(x =>
                        {
                            x[1] = sourceEntity;
                            return true;
                        });


            var pbRaycast = new PBRaycast { TargetEntity = (uint)targetCrdtEntity.Id };

            // Act: Create the ray using the source entity's transform
            bool success = pbRaycast.TryCreateRay(world, entitiesMap, Vector3.zero, in sourceTs, out Ray ray);

            // Assert
            Assert.That(success, Is.True);

            // Ray origin should be the source's *updated* position
            Assert.That(ray.origin, Is.EqualTo(updatedSourcePos));

            // Ray direction should be from updated source to updated target
            var expectedDirection = (updatedTargetPos - updatedSourcePos).normalized;
            Assert.That(ray.direction, Is.EqualTo(expectedDirection));
        }
    }
}
