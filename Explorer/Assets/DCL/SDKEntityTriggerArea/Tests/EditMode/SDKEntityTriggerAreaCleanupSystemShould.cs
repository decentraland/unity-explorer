using Arch.Core;
using DCL.SDKEntityTriggerArea.Components;
using DCL.SDKEntityTriggerArea.Systems;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea.Tests
{
    public class SDKEntityTriggerAreaCleanupSystemShould : UnitySystemTestBase<SDKEntityTriggerAreaCleanupSystem>
    {
        private Entity entity;
        private GameObject fakeAvatarGO;
        private GameObject fakeTriggerAreaGO;
        private SDKEntityTriggerArea sdkEntityTriggerArea;
        private IComponentPool<SDKEntityTriggerArea> poolRegistry;

        [SetUp]
        public void Setup()
        {
            entity = world.Create(PartitionComponent.TOP_PRIORITY);

            fakeAvatarGO = new GameObject();
            fakeTriggerAreaGO = new GameObject();
            sdkEntityTriggerArea = fakeTriggerAreaGO.AddComponent<SDKEntityTriggerArea>();

            poolRegistry = Substitute.For<IComponentPool<SDKEntityTriggerArea>>();
            poolRegistry.Get().Returns(sdkEntityTriggerArea);
            system = new SDKEntityTriggerAreaCleanupSystem(world, poolRegistry);
        }

        protected override void OnTearDown()
        {
            poolRegistry.Dispose();
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
        }

        [Test]
        public void HandleCameraModeAreaComponentRemoveCorrectly()
        {
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, monoBehaviour: sdkEntityTriggerArea);
            world.Add(entity, component, new PBCameraModeArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(sdkEntityTriggerArea);
            Assert.IsTrue(world.Has<SDKEntityTriggerAreaComponent>(entity));

            // Remove component
            world.Remove<PBCameraModeArea>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(sdkEntityTriggerArea);
            Assert.IsFalse(world.Has<SDKEntityTriggerAreaComponent>(entity));
        }

        [Test]
        public void HandleAvatarModifierAreaComponentRemoveCorrectly()
        {
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, monoBehaviour: sdkEntityTriggerArea);
            world.Add(entity, component, new PBAvatarModifierArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(sdkEntityTriggerArea);
            Assert.IsTrue(world.Has<SDKEntityTriggerAreaComponent>(entity));

            // Remove component
            world.Remove<PBAvatarModifierArea>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(sdkEntityTriggerArea);
            Assert.IsFalse(world.Has<SDKEntityTriggerAreaComponent>(entity));
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            var component = new SDKEntityTriggerAreaComponent(areaSize: Vector3.one * 4, monoBehaviour: sdkEntityTriggerArea);
            world.Add(entity, component, new PBAvatarModifierArea());

            system.Update(0);

            poolRegistry.DidNotReceive().Release(sdkEntityTriggerArea);
            Assert.IsTrue(world.Has<SDKEntityTriggerAreaComponent>(entity));

            // Flag entity deletion
            world.Add<DeleteEntityIntention>(entity);
            system.Update(0);

            poolRegistry.Received(1).Release(sdkEntityTriggerArea);
        }
    }
}
