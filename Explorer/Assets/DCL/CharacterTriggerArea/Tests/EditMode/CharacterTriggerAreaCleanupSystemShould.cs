using Arch.Core;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Tests
{
    public class CharacterTriggerAreaCleanupSystemShould : UnitySystemTestBase<CharacterTriggerAreaCleanupSystem>
    {
        private Entity entity;
        private GameObject fakeAvatarGO;
        private GameObject fakeTriggerAreaGO;
        private CharacterTriggerArea characterTriggerArea;
        private IComponentPool<CharacterTriggerArea> poolRegistry;


        public void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY);

            fakeAvatarGO = new GameObject();
            fakeTriggerAreaGO = new GameObject();
            characterTriggerArea = fakeTriggerAreaGO.AddComponent<CharacterTriggerArea>();

            poolRegistry = Substitute.For<IComponentPool<CharacterTriggerArea>>();
            poolRegistry.Get().Returns(characterTriggerArea);
            system = new CharacterTriggerAreaCleanupSystem(world, poolRegistry);
        }


        public void Teardown()
        {
            poolRegistry.Dispose();
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
        }


        public void ClearEnterExitCollectionsCorrectly()
        {
            var component = new CharacterTriggerAreaComponent(Vector3.one);
            component.MonoBehaviour = characterTriggerArea;
            world.Add(entity, component, new PBCameraModeArea());

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
        }


        public void HandleCameraModeAreaComponentRemoveCorrectly()
        {
            var component = new CharacterTriggerAreaComponent(areaSize: Vector3.one * 4);
            component.MonoBehaviour = characterTriggerArea;
            world.Add(entity, component, new PBCameraModeArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(characterTriggerArea);
            Assert.IsTrue(world.Has<CharacterTriggerAreaComponent>(entity));

            // Remove component
            world.Remove<PBCameraModeArea>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(characterTriggerArea);
            Assert.IsFalse(world.Has<CharacterTriggerAreaComponent>(entity));
        }


        public void HandleAvatarModifierAreaComponentRemoveCorrectly()
        {
            var component = new CharacterTriggerAreaComponent(areaSize: Vector3.one * 4);
            component.MonoBehaviour = characterTriggerArea;
            world.Add(entity, component, new PBAvatarModifierArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(characterTriggerArea);
            Assert.IsTrue(world.Has<CharacterTriggerAreaComponent>(entity));

            // Remove component
            world.Remove<PBAvatarModifierArea>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(characterTriggerArea);
            Assert.IsFalse(world.Has<CharacterTriggerAreaComponent>(entity));
        }


        public void HandleEntityDestructionCorrectly()
        {
            var component = new CharacterTriggerAreaComponent(areaSize: Vector3.one * 4);
            component.MonoBehaviour = characterTriggerArea;
            world.Add(entity, component, new PBAvatarModifierArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(characterTriggerArea);
            Assert.IsTrue(world.Has<CharacterTriggerAreaComponent>(entity));

            // Flag entity deletion
            world.Add<DeleteEntityIntention>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(characterTriggerArea);
            Assert.IsFalse(world.Has<CharacterTriggerAreaComponent>(entity));
        }
    }
}
