using Arch.Core;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.ECSComponents;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Tests
{
    public class CharacterTriggerAreaCleanUpRegisteredCollisionsSystemShould
        : UnitySystemTestBase<CharacterTriggerAreaCleanUpRegisteredCollisionsSystem>
    {
        private CharacterTriggerArea characterTriggerArea;
        private Entity entity;
        private GameObject fakeAvatarGO;
        private GameObject fakeTriggerAreaGO;

        [SetUp]
        public void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY);

            fakeAvatarGO = new GameObject();
            fakeTriggerAreaGO = new GameObject();
            characterTriggerArea = fakeTriggerAreaGO.AddComponent<CharacterTriggerArea>();
            system = new CharacterTriggerAreaCleanUpRegisteredCollisionsSystem(world);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
        }

        [Test]
        public void ClearEnterExitCollectionsCorrectly()
        {
            var component = new CharacterTriggerAreaComponent(Vector3.one, monoBehaviour: characterTriggerArea);
            world.Add(entity, component, new PBCameraModeArea());

            characterTriggerArea.enteredThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.EnteredThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);

            characterTriggerArea.exitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.ExitedThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);

            characterTriggerArea.enteredThisFrame.Add(fakeAvatarGO.transform);
            characterTriggerArea.exitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.EnteredThisFrame.Count);
            Assert.AreEqual(1, component.ExitedThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);
        }
    }
}
