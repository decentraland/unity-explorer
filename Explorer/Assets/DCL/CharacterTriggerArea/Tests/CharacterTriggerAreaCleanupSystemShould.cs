using Arch.Core;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Tests
{
    public class CharacterTriggerAreaCleanupSystemShould : UnitySystemTestBase<CharacterTriggerAreaCleanupSystem>
    {
        [Test]
        public void ClearEnterExitCollectionsCorrectly()
        {
            var fakeAvatarGO = new GameObject();
            var fakeTriggerAreaGO = new GameObject();

            system = new CharacterTriggerAreaCleanupSystem(world);
            Entity entity = world.Create();
            var component = new CharacterTriggerAreaComponent(Vector3.one);
            component.MonoBehaviour = fakeTriggerAreaGO.AddComponent<CharacterTriggerArea>();
            world.Add(entity, component);

            component.MonoBehaviour.enteredThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.EnteredThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);

            component.MonoBehaviour.exitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.ExitedThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);

            component.MonoBehaviour.enteredThisFrame.Add(fakeAvatarGO.transform);
            component.MonoBehaviour.exitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);
            Assert.AreEqual(1, component.EnteredThisFrame.Count);
            Assert.AreEqual(1, component.ExitedThisFrame.Count);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);

            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
        }
    }
}
