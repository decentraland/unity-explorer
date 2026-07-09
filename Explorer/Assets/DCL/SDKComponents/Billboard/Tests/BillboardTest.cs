using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Billboard.System;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;
using DCL.Billboard.Demo.World;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.PerformanceTesting;
using Unity.Profiling;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.Billboard.Tests
{
    public class BillboardTest
    {
        private static readonly IReadOnlyDictionary<CRDTEntity, Entity> EMPTY_ENTITIES_MAP = new Dictionary<CRDTEntity, Entity>();

        [Test]
        public void NoRotation()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmNone);

            var expected = transform.rotation;
            system.Update(0);
            Assert.AreEqual(expected, transform.rotation);
        }

        [Test]
        public void XRotation()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmX);

            var expected = transform.rotation.eulerAngles;
            system.Update(0);
            var actual = transform.rotation.eulerAngles;
            Assert.AreNotEqual(expected.x, actual.x);
            Assert.AreEqual(180f, actual.y); //todo investigate why it rotates to 180
            Assert.AreEqual(expected.z, actual.z);
        }

        [Test]
        public void YRotation()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY);

            var expected = transform.rotation.eulerAngles;
            system.Update(0);
            var actual = transform.rotation.eulerAngles;
            Assert.AreNotEqual(expected.y, actual.y);
            Assert.AreEqual(expected.x, actual.x);
            Assert.AreEqual(expected.z, actual.z);
        }

        [Test]
        public void ZRotation()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmZ);

            var expected = transform.rotation.eulerAngles;
            system.Update(0);
            var actual = transform.rotation.eulerAngles;
            Assert.AreNotEqual(expected.z, actual.z);
            Assert.AreEqual(expected.x, actual.x);
            Assert.AreEqual(expected.y, actual.y);
        }

        [Test]
        public void AllRotation()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll);

            var expected = transform.rotation.eulerAngles;
            system.Update(0);
            var actual = transform.rotation.eulerAngles;
            Assert.AreNotEqual(expected.x, actual.x);
            Assert.AreNotEqual(expected.y, actual.y);
            Assert.AreNotEqual(expected.z, actual.z);
        }

        [Test]
        public void EnforceMinimumDistance()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll, Vector3.one * 0.1f);

            var expected = transform.rotation.eulerAngles;
            system.Update(0);
            var actual = transform.rotation.eulerAngles;
            Assert.AreEqual(expected.x, actual.x);
            Assert.AreEqual(expected.y, actual.y);
            Assert.AreEqual(expected.z, actual.z);
        }

        [Test]
        [Performance]
        [TestCase(200)]
        [TestCase(500)]
        [TestCase(1000)]
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public void BillboardPerformance(int randomCounts)
        {
            var world = new BillboardDemoWorld(
                World.Create(),
                cameraData: new IExposedCameraData.Random(),
                randomCounts: randomCounts
            );

            world.SetUp();

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure
               .Method(world.Update)
               .GC()
               .Run();

            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Debug.Log($"[BillboardSystem] {randomCounts} entities — GC.Alloc last: {gcBytes} bytes");
            Assert.That(gcBytes, Is.EqualTo(0), $"BillboardSystem.Update must be allocation-free, but allocated {gcBytes} bytes with {randomCounts} entities");
        }

        [Test]
        public void TargetEntityOrientsTowardTarget()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY, new Vector3(10, 0, 0), map, 100);
            map[new CRDTEntity(100)] = CreateTarget(world, new Vector3(0, 0, 10));

            system.Update(0);

            // forward matches direction from target to billboard (y = 180), not from camera (y = 270)
            Assert.That(transform.rotation.eulerAngles.y, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void UnresolvedTargetDisablesBillboard()
        {
            (_, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll, targetEntity: 100);
            transform.rotation = Quaternion.Euler(10, 20, 30);
            var expected = transform.rotation;

            system.Update(0);

            Assert.AreEqual(expected, transform.rotation);
        }

        [Test]
        public void TargetResolvedOncePresent()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY, entitiesMap: map, targetEntity: 100);
            var initial = transform.rotation;

            system.Update(0);
            Assert.AreEqual(initial, transform.rotation);

            map[new CRDTEntity(100)] = CreateTarget(world, new Vector3(0, 0, 10));

            system.Update(0);
            Assert.That(transform.rotation.eulerAngles.y, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void CameraSentinelUsesCameraPath()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY, new Vector3(10, 0, 0), map, (uint)SpecialEntitiesID.CAMERA_ENTITY);
            map[new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY)] = CreateTarget(world, new Vector3(0, 0, 10));

            system.Update(0);

            // camera path: forward matches direction from camera to billboard (y = 270), decoy at CRDTEntity(2) ignored
            Assert.That(transform.rotation.eulerAngles.y, Is.EqualTo(270f).Within(0.01f));
        }

        [Test]
        public void SelfTargetDisablesBillboard()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (_, Entity entity, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll, entitiesMap: map, targetEntity: 100);
            map[new CRDTEntity(100)] = entity;
            transform.rotation = Quaternion.Euler(10, 20, 30);
            var expected = transform.rotation;

            system.Update(0);

            Assert.AreEqual(expected, transform.rotation);
        }

        [Test]
        public void TargetDeletedDisablesBillboard()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY, entitiesMap: map, targetEntity: 100);
            Entity target = CreateTarget(world, new Vector3(0, 0, 10));
            map[new CRDTEntity(100)] = target;

            system.Update(0);
            var resolved = transform.rotation;
            Assert.That(resolved.eulerAngles.y, Is.EqualTo(180f).Within(0.01f));

            map.Remove(new CRDTEntity(100));
            world.Destroy(target);

            system.Update(0);
            Assert.AreEqual(resolved, transform.rotation);
        }

        [Test]
        public void EnforceMinimumDistanceToTarget()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll, new Vector3(100, 0, 0), map, 100);
            map[new CRDTEntity(100)] = CreateTarget(world, new Vector3(0.1f, 0, 0));
            var expected = transform.rotation;

            system.Update(0);

            Assert.AreEqual(expected, transform.rotation);
        }

        [Test]
        public void ZRotationFromTargetRoll()
        {
            var map = new Dictionary<CRDTEntity, Entity>();
            (World world, _, Transform transform, BillboardSystem system) = Construct(BillboardMode.BmZ, entitiesMap: map, targetEntity: 100);
            map[new CRDTEntity(100)] = CreateTarget(world, new Vector3(0, 0, 10), Quaternion.Euler(0, 0, 45f));

            system.Update(0);

            // roll comes from the target's Z euler (45), not the camera's (1)
            Assert.That(transform.rotation.eulerAngles.z, Is.EqualTo(45f).Within(0.01f));
        }

        private static (World world, Entity entity, Transform transform, BillboardSystem system) Construct(
            BillboardMode mode,
            Vector3? cameraPos = null,
            IReadOnlyDictionary<CRDTEntity, Entity>? entitiesMap = null,
            uint? targetEntity = null
        )
        {
            var world = World.Create();

            var system = new BillboardSystem(
                world,
                new IExposedCameraData.Fake(
                    cameraPos ?? Vector3.one,
                    Quaternion.Euler(Vector3.one),
                    CameraType.CtFirstPerson,
                    false
                ),
                entitiesMap ?? EMPTY_ENTITIES_MAP
            );

            var transform = new GameObject().transform;
            transform.position = Vector3.zero;

            var billboard = new PBBillboard { BillboardMode = mode };

            if (targetEntity.HasValue)
                billboard.TargetEntity = targetEntity.Value;

            Entity entity = world.Create(
                billboard,
                new TransformComponent(transform)
            );

            return (world, entity, transform, system);
        }

        private static Entity CreateTarget(World world, Vector3 position, Quaternion? rotation = null)
        {
            var targetTransform = new GameObject().transform;
            targetTransform.position = position;

            if (rotation.HasValue)
                targetTransform.rotation = rotation.Value;

            return world.Create(new TransformComponent(targetTransform));
        }
    }
}
