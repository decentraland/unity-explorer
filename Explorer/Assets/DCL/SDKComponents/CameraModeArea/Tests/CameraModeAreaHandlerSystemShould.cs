using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
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
            var globalWorldProxy = new WorldProxy();
            globalWorldProxy.SetWorld(globalWorld);

            cameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent { Mode = CameraMode.ThirdPerson }
            );

            var cameraEntityProxy = new EntityProxy();
            cameraEntityProxy.SetEntity(cameraEntity);

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

            // enter trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // exit trigger area
            system.OnExitedCameraModeArea(null);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(originalCameraMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);

            // enter trigger area again
            system.OnEnteredCameraModeArea(firstTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // enter 2nd trigger are without exiting the previous one
            CameraMode secondTriggerAreaMode = CameraMode.Free;
            system.OnEnteredCameraModeArea(secondTriggerAreaMode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // exit 1st trigger area
            system.OnExitedCameraModeArea(null);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // exit last trigger area
            system.OnExitedCameraModeArea(null);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);
        }
    }
}
