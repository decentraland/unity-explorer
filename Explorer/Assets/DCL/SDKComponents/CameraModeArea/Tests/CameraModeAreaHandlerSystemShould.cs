using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.CameraModeArea.Tests
{
    public class CameraModeAreaHandlerSystemShould : UnitySystemTestBase<CameraModeAreaHandlerSystem>
    {
        private Entity entity;
        private Entity cameraEntity;
        private World globalWorld;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            var globalWorldProxy = new ObjectProxy<World>();
            globalWorldProxy.SetObject(globalWorld);

            cameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent { Mode = CameraMode.ThirdPerson }
            );

            var cameraEntityProxy = new ObjectProxy<Entity>();
            cameraEntityProxy.SetObject(cameraEntity);

            system = new CameraModeAreaHandlerSystem(world, globalWorldProxy, cameraEntityProxy);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        [Test]
        public void SetupCharacterTriggerAreaCorrectly()
        {
            var areaSize = new Vector3
            {
                X = 2.5f,
                Y = 3.8f,
                Z = 8.5f,
            };

            var component = new PBCameraModeArea
            {
                Area = areaSize,
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out CharacterTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void UpdateCharacterTriggerAreaCorrectly()
        {
            var areaSize = new Vector3
            {
                X = 5.2f,
                Y = 8.3f,
                Z = 5.8f,
            };

            var component = new PBCameraModeArea
            {
                Area = areaSize,
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out CharacterTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);

            // update component
            areaSize.X *= 2.5f;
            areaSize.Y /= 1.3f;
            areaSize.Z /= 6.6f;
            component.Area = areaSize;
            component.IsDirty = true;
            world.Set(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void SetupCameraModeAreaComponentCorrectly()
        {
            var component = new PBCameraModeArea
            {
                Area = new Vector3
                {
                    X = 2.5f,
                    Y = 3.8f,
                    Z = 8.5f,
                },
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));
        }

        [Test]
        public void UpdateCameraModeOnTriggerAreaEnter()
        {
            system.OnEnteredCameraModeArea(CameraMode.FirstPerson);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            Assert.AreEqual(CameraMode.FirstPerson, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            system.OnEnteredCameraModeArea(CameraMode.ThirdPerson);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(CameraMode.ThirdPerson, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);
        }

        [Test]
        public void HandleCameraModeResetCorrectlyOnTriggerAreaExit()
        {
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            CameraMode originalCameraMode = cameraComponent.Mode;

            // "Enter" trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" trigger area
            system.OnExitedCameraModeArea();
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(originalCameraMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);

            // "Enter" trigger area again
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Enter" 2nd trigger are without exiting the previous one
            CameraMode secondTriggerAreaMode = CameraMode.Free;
            system.OnEnteredCameraModeArea(secondTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" 1st trigger area
            system.OnExitedCameraModeArea();
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" last trigger area
            system.OnExitedCameraModeArea();
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            CameraMode originalCameraMode = cameraComponent.Mode;

            var component = new PBCameraModeArea
            {
                Area = new Vector3
                {
                    X = 2.5f,
                    Y = 3.8f,
                    Z = 8.5f,
                },
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));

            // "Enter" trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // Remove component
            world.Remove<PBCameraModeArea>(entity);
            system.Update(1);

            // Check trigger area effect was reset
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(originalCameraMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);

            Assert.IsFalse(world.Has<CameraModeAreaComponent>(entity));
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            CameraMode originalCameraMode = cameraComponent.Mode;

            var component = new PBCameraModeArea
            {
                Area = new Vector3
                {
                    X = 2.5f,
                    Y = 3.8f,
                    Z = 8.5f,
                },
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));

            // "Enter" trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // Flag entity for destruction
            world.Add<DeleteEntityIntention>(entity);
            system.Update(1);

            // Check trigger area effect was reset
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(originalCameraMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);

            Assert.IsFalse(world.Has<CameraModeAreaComponent>(entity));
        }
    }
}
