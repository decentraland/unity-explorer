using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.SpringBones.Tests
{
    public class SpringBoneServiceShould
    {
        private SpringBoneService service;
        private readonly List<GameObject> createdGameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            service = new SpringBoneService();
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();

            foreach (GameObject go in createdGameObjects)
                if (go != null) Object.DestroyImmediate(go);

            createdGameObjects.Clear();
        }

        private Transform MakeTransform(string name = "joint")
        {
            var go = new GameObject(name);
            createdGameObjects.Add(go);
            return go.transform;
        }

        private (List<Transform> joints, List<SpringBoneJointConfig> configs, List<float3> tails) MakeChain(int jointCount)
        {
            var joints = new List<Transform>();
            var configs = new List<SpringBoneJointConfig>();
            var tails = new List<float3>();

            for (int i = 0; i < jointCount; i++)
            {
                Transform t = MakeTransform($"joint_{i}");
                t.position = new Vector3(0, i * 0.1f, 0);
                joints.Add(t);
                configs.Add(new SpringBoneJointConfig { Stiffness = 0.5f, Drag = 0.1f, LocalRotation = quaternion.identity });
                tails.Add(new float3(0, i * 0.1f, 0));
            }

            return (joints, configs, tails);
        }

        [Test]
        public void RegisterReturnsUniqueSlot()
        {
            var (joints1, configs1, tails1) = MakeChain(3);
            var (joints2, configs2, tails2) = MakeChain(3);

            int slot1 = service.RegisterSpring(joints1, configs1, tails1);
            int slot2 = service.RegisterSpring(joints2, configs2, tails2);

            Assert.AreNotEqual(slot1, slot2);
            Assert.GreaterOrEqual(slot1, 0);
            Assert.GreaterOrEqual(slot2, 0);
        }

        [Test]
        public void UnregisterReleasesSlotForReuse()
        {
            var (joints1, configs1, tails1) = MakeChain(3);
            int slot1 = service.RegisterSpring(joints1, configs1, tails1);

            service.UnregisterSpring(slot1);

            var (joints2, configs2, tails2) = MakeChain(3);
            int slot2 = service.RegisterSpring(joints2, configs2, tails2);

            Assert.AreEqual(slot1, slot2, "Freed slot should be reused (LIFO free list)");
        }

        [Test]
        public void UnregisterClearsActiveFlag()
        {
            var (joints, configs, tails) = MakeChain(3);
            int slot = service.RegisterSpring(joints, configs, tails);
            service.SetSlotActive(slot, true);

            Assert.IsTrue(service.IsSlotActive(slot));

            service.UnregisterSpring(slot);

            Assert.IsFalse(service.IsSlotActive(slot), "Unregister must clear slotActive");
        }

        [Test]
        public void GrowDoublesCapacityWhenFull()
        {
            int initial = service.SlotCapacity;

            for (int i = 0; i < initial; i++)
            {
                var (joints, configs, tails) = MakeChain(2);
                service.RegisterSpring(joints, configs, tails);
            }

            Assert.AreEqual(0, service.FreeSlotsCount, "All initial slots consumed");

            // One more registration triggers Grow
            var (extraJoints, extraConfigs, extraTails) = MakeChain(2);
            int extraSlot = service.RegisterSpring(extraJoints, extraConfigs, extraTails);

            Assert.AreEqual(initial * 2, service.SlotCapacity, "Grow doubles capacity");
            Assert.GreaterOrEqual(extraSlot, 0);
            // After Grow + one registration, newly added slots minus the one just consumed remain free
            Assert.AreEqual(initial - 1, service.FreeSlotsCount);
        }

        [Test]
        public void DeactivateAllSlotsClearsEveryActiveFlag()
        {
            var (joints1, configs1, tails1) = MakeChain(2);
            var (joints2, configs2, tails2) = MakeChain(2);
            int slot1 = service.RegisterSpring(joints1, configs1, tails1);
            int slot2 = service.RegisterSpring(joints2, configs2, tails2);

            service.SetSlotActive(slot1, true);
            service.SetSlotActive(slot2, true);
            service.DeactivateAllSlots();

            Assert.IsFalse(service.IsSlotActive(slot1));
            Assert.IsFalse(service.IsSlotActive(slot2));
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            service.Dispose();
            Assert.IsTrue(service.IsDisposed);

            // Second dispose must not throw
            Assert.DoesNotThrow(() => service.Dispose());
        }

        [Test]
        public void UnregisterAfterDisposeIsSafe()
        {
            var (joints, configs, tails) = MakeChain(2);
            int slot = service.RegisterSpring(joints, configs, tails);

            service.Dispose();

            // World system cleanup may run after plugin dispose — must not throw
            Assert.DoesNotThrow(() => service.UnregisterSpring(slot));
        }

        [Test]
        public void SimulateWithZeroActiveSlotsDoesNotThrow()
        {
            var (joints, configs, tails) = MakeChain(3);
            service.RegisterSpring(joints, configs, tails);
            // slot left inactive

            Assert.DoesNotThrow(() =>
            {
                service.PrepareSimulation();
                service.Simulate(0.016f);
            });
        }

        [Test]
        public void SimulateActiveSlotProducesFiniteRotations()
        {
            var (joints, configs, tails) = MakeChain(3);
            int slot = service.RegisterSpring(joints, configs, tails);

            service.SetParentData(slot, quaternion.identity, float4x4.identity);
            service.SetSlotActive(slot, true);
            service.PrepareSimulation();
            service.Simulate(0.016f);

            foreach (Transform t in joints)
            {
                Quaternion r = t.rotation;
                Assert.IsFalse(float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.z) || float.IsNaN(r.w),
                    "Simulation must not produce NaN rotations");
            }
        }

        [Test]
        public void RegisterOverwritesSlotContentsAfterReuse()
        {
            var (joints1, configs1, tails1) = MakeChain(5);
            int slotA = service.RegisterSpring(joints1, configs1, tails1);
            service.UnregisterSpring(slotA);

            // Shorter chain reuses slot; trailing joints must be dummies so push/pull skip them
            var (joints2, configs2, tails2) = MakeChain(2);
            int slotB = service.RegisterSpring(joints2, configs2, tails2);

            Assert.AreEqual(slotA, slotB);
            // Drive a sim step — trailing dummy transforms must not throw
            service.SetParentData(slotB, quaternion.identity, float4x4.identity);
            service.SetSlotActive(slotB, true);
            service.PrepareSimulation();
            Assert.DoesNotThrow(() => service.Simulate(0.016f));
        }
    }
}
