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
            system = new CharacterTriggerAreaCleanupSystem(world);
            Entity entity = world.Create();
            var component = new CharacterTriggerAreaComponent(Vector3.one);
            world.Add(entity, component);

            var fakeAvatarGO = new GameObject();

            component.EnteredThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);

            component.ExitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);

            system.Update(0);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);

            component.EnteredThisFrame.Add(fakeAvatarGO.transform);
            component.ExitedThisFrame.Add(fakeAvatarGO.transform);
            world.Set(entity, component);

            system.Update(0);
            Assert.AreEqual(0, component.EnteredThisFrame.Count);
            Assert.AreEqual(0, component.ExitedThisFrame.Count);

            Object.DestroyImmediate(fakeAvatarGO);
        }
    }
}
