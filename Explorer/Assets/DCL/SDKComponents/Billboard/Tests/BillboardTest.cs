using Arch.Core;
using DCL.Billboard.System;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;
using DCL.Billboard.Demo.World;
using System.Diagnostics.CodeAnalysis;
using Unity.PerformanceTesting;
using Unity.Profiling;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.Billboard.Tests
{
    public class BillboardTest
    {
        [Test]
        public void NoRotation()
        {
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmNone);

            var expected = transform.rotation;
            system.Update(0);
            Assert.AreEqual(expected, transform.rotation);
        }

        [Test]
        public void XRotation()
        {
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmX);

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
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmY);

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
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmZ);

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
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll);

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
            (Transform transform, BillboardSystem system) = Construct(BillboardMode.BmAll, Vector3.one * 0.1f);

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

        private static (Transform transform, BillboardSystem system) Construct(BillboardMode mode, Vector3? cameraPos = null)
        {
            var world = World.Create();

            var system = new BillboardSystem(
                world,
                new IExposedCameraData.Fake(
                    cameraPos ?? Vector3.one,
                    Quaternion.Euler(Vector3.one),
                    CameraType.CtFirstPerson,
                    false
                )
            );

            var transform = new GameObject().transform;
            transform.position = Vector3.zero;

            world.Create(
                new PBBillboard { BillboardMode = mode },
                new TransformComponent(transform)
            );

            return (transform, system);
        }
    }
}
