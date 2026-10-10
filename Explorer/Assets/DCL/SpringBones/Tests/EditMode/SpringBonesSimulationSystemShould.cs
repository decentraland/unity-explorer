using Arch.Core;
using DCL.Character.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.SpringBones.Tests
{
    public class SpringBonesSimulationSystemShould : UnitySystemTestBase<SpringBonesSimulationSystem>
    {
        private SpringBoneService service;
        private SpringBoneSimulationSettings settings;
        private readonly List<GameObject> createdGameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            service = new SpringBoneService();
            settings = new SpringBoneSimulationSettings { SimulationEnabled = true, MaxSimulatedAvatars = 10 };
            system = new SpringBonesSimulationSystem(world, service, settings);
        }

        protected override void OnTearDown()
        {
            service.Dispose();

            foreach (GameObject go in createdGameObjects)
                if (go != null) Object.DestroyImmediate(go);

            createdGameObjects.Clear();
        }

        private Transform MakeTransform(string name)
        {
            var go = new GameObject(name);
            createdGameObjects.Add(go);
            return go.transform;
        }

        private int RegisterDummySlot()
        {
            var joints = new List<Transform> { MakeTransform("joint") };
            var configs = new List<SpringBoneJointConfig> { new () { LocalRotation = quaternion.identity } };
            var tails = new List<float3> { float3.zero };
            return service.RegisterSpring(joints, configs, tails);
        }

        private Entity CreateAvatarEntity(float sqrDistance, bool isBehind = false, bool isPlayer = false)
        {
            int slotIndex = RegisterDummySlot();
            var slots = new List<SpringBoneSlot>
            {
                new ()
                {
                    SlotIndex = slotIndex,
                    WearableParent = MakeTransform($"wearableParent_{slotIndex}"),
                    AvatarParent = MakeTransform($"avatarParent_{slotIndex}"),
                },
            };

            var registration = new SpringBoneRegistrationComponent { Slots = slots, AvatarVersion = 0 };

            if (isPlayer)
                return world.Create(registration, new PlayerComponent());

            var partition = new PartitionComponent { RawSqrDistance = sqrDistance, IsBehind = isBehind };
            return world.Create(registration, partition);
        }

        [Test]
        public void DeactivateAllSlotsWhenSimulationDisabled()
        {
            int slot = RegisterDummySlot();
            service.SetSlotActive(slot, true);

            settings.SimulationEnabled = false;
            system.Update(0.016f);

            Assert.IsFalse(service.IsSlotActive(slot));
        }

        [Test]
        public void ActivateNearestAvatarsUpToMaxLimit()
        {
            settings.MaxSimulatedAvatars = 2;

            Entity near = CreateAvatarEntity(1f);
            Entity mid = CreateAvatarEntity(4f);
            Entity far1 = CreateAvatarEntity(100f);
            Entity far2 = CreateAvatarEntity(200f);

            system.Update(0.016f);

            int nearSlot = world.Get<SpringBoneRegistrationComponent>(near).Slots[0].SlotIndex;
            int midSlot = world.Get<SpringBoneRegistrationComponent>(mid).Slots[0].SlotIndex;
            int far1Slot = world.Get<SpringBoneRegistrationComponent>(far1).Slots[0].SlotIndex;
            int far2Slot = world.Get<SpringBoneRegistrationComponent>(far2).Slots[0].SlotIndex;

            Assert.IsTrue(service.IsSlotActive(nearSlot), "Nearest avatar should be active");
            Assert.IsTrue(service.IsSlotActive(midSlot), "Second nearest avatar should be active");
            Assert.IsFalse(service.IsSlotActive(far1Slot), "Farther avatar beyond limit must be inactive");
            Assert.IsFalse(service.IsSlotActive(far2Slot), "Farthest avatar beyond limit must be inactive");
        }

        [Test]
        public void SkipAvatarsBehindCamera()
        {
            settings.MaxSimulatedAvatars = 10;

            Entity front = CreateAvatarEntity(5f, isBehind: false);
            Entity behind = CreateAvatarEntity(1f, isBehind: true);

            system.Update(0.016f);

            int frontSlot = world.Get<SpringBoneRegistrationComponent>(front).Slots[0].SlotIndex;
            int behindSlot = world.Get<SpringBoneRegistrationComponent>(behind).Slots[0].SlotIndex;

            Assert.IsTrue(service.IsSlotActive(frontSlot));
            Assert.IsFalse(service.IsSlotActive(behindSlot), "IsBehind avatars must be skipped regardless of distance");
        }

        [Test]
        public void AlwaysSimulatePlayerAvatarRegardlessOfOtherDistances()
        {
            settings.MaxSimulatedAvatars = 1;

            // Remote avatar is far — would be the only candidate normally
            Entity remote = CreateAvatarEntity(999f);
            Entity player = CreateAvatarEntity(0f, isPlayer: true);

            system.Update(0.016f);

            int playerSlot = world.Get<SpringBoneRegistrationComponent>(player).Slots[0].SlotIndex;
            // Player gets top priority (distance 0) so must be one of the activated
            Assert.IsTrue(service.IsSlotActive(playerSlot), "Player avatar must always be activated");
        }

        [Test]
        public void HandleEmptyWorldGracefully()
        {
            Assert.DoesNotThrow(() => system.Update(0.016f));
        }

        [Test]
        public void SkipRegistrationsWithNullOrEmptySlots()
        {
            world.Create(new SpringBoneRegistrationComponent { Slots = null }, PartitionComponent.TOP_PRIORITY);
            world.Create(new SpringBoneRegistrationComponent { Slots = new List<SpringBoneSlot>() }, PartitionComponent.TOP_PRIORITY);

            Assert.DoesNotThrow(() => system.Update(0.016f));
        }
    }
}