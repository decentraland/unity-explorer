using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.SDKEntityTriggerArea.Components;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.CameraModeArea.Tests
{
    public class CameraModeAreaHandlerSystemShould : UnitySystemTestBase<CameraModeAreaHandlerSystem>
    {
        private const int AREA_CRDT_ID = 512;
        private const int OTHER_AREA_CRDT_ID = 513;

        private static readonly UnityEngine.Vector3 AREA_SIZE = new (2.5f, 3.8f, 8.5f);

        private Entity entity;
        private Entity otherEntity;
        private Entity cameraEntity;
        private World globalWorld = null!;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();

            cameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent { Mode = CameraMode.ThirdPerson }
            );

            var cameraEntityProxy = new ObjectProxy<Entity>();
            cameraEntityProxy.SetObject(cameraEntity);

            system = new CameraModeAreaHandlerSystem(world, globalWorld, cameraEntityProxy, Substitute.For<IExposedCameraData>(), Substitute.For<ISceneRestrictionBusController>(), new ISceneData.Fake());

            entity = world.Create(PartitionComponent.TOP_PRIORITY, new CRDTEntity(AREA_CRDT_ID));
            AddTransformToEntity(entity);

            otherEntity = world.Create(PartitionComponent.TOP_PRIORITY, new CRDTEntity(OTHER_AREA_CRDT_ID));
            AddTransformToEntity(otherEntity);
        }

        [TearDown]
        public void TearDown()
        {
            // The active-area list is static: leave nothing behind for the next fixture.
            system.OnExitedCameraModeArea(entity);
            system.OnExitedCameraModeArea(otherEntity);
            globalWorld.Dispose();
        }

        [Test]
        public void SetupSDKEntityTriggerAreaCorrectly()
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

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));
            Assert.IsTrue(world.TryGet(entity, out SDKEntityTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void UpdateSDKEntityTriggerAreaCorrectly()
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

            Assert.IsTrue(world.TryGet(entity, out SDKEntityTriggerAreaComponent triggerAreaComponent));
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
            world.Add(entity, CreateComponent());

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));
        }

        [Test]
        public void UpdateCameraModeOnTriggerAreaEnter()
        {
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            Assert.AreEqual(CameraMode.FirstPerson, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            system.OnEnteredCameraModeArea(otherEntity, OTHER_AREA_CRDT_ID, CameraMode.ThirdPerson, AREA_SIZE);
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
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, firstTriggerAreaMode, AREA_SIZE);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" trigger area
            system.OnExitedCameraModeArea(entity);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(originalCameraMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);

            // "Enter" trigger area again
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, firstTriggerAreaMode, AREA_SIZE);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Enter" 2nd trigger are without exiting the previous one
            CameraMode secondTriggerAreaMode = CameraMode.Free;
            system.OnEnteredCameraModeArea(otherEntity, OTHER_AREA_CRDT_ID, secondTriggerAreaMode, AREA_SIZE);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" 1st trigger area
            system.OnExitedCameraModeArea(entity);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(secondTriggerAreaMode, cameraComponent.Mode);
            Assert.IsFalse(cameraComponent.CameraInputChangeEnabled);

            // "Exit" last trigger area
            system.OnExitedCameraModeArea(otherEntity);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out cameraComponent));
            Assert.AreEqual(firstTriggerAreaMode, cameraComponent.Mode);
            Assert.IsTrue(cameraComponent.CameraInputChangeEnabled);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            CameraMode originalCameraMode = cameraComponent.Mode;

            world.Add(entity, CreateComponent());

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));

            // "Enter" trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, firstTriggerAreaMode, AREA_SIZE);
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

            world.Add(entity, CreateComponent());

            system.Update(1);

            Assert.IsTrue(world.Has<CameraModeAreaComponent>(entity));

            // "Enter" trigger area
            CameraMode firstTriggerAreaMode = CameraMode.FirstPerson;
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, firstTriggerAreaMode, AREA_SIZE);
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
        }

        [Test]
        public void PublishTheLockOnEnterAndRemoveItOnExit()
        {
            // Act
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Assert
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraModeAreaLock areaLock));
            Assert.AreEqual("Fake", areaLock.SceneName);
            Assert.AreEqual(UnityEngine.Vector2Int.zero, areaLock.SceneBaseParcel);
            Assert.AreEqual(AREA_CRDT_ID, areaLock.CrdtEntityId);
            Assert.AreEqual(CameraMode.FirstPerson, areaLock.Mode);
            Assert.AreEqual(AREA_SIZE, areaLock.AreaSize);
            Assert.AreEqual(1, areaLock.ActiveAreaCount);

            // Act
            system.OnExitedCameraModeArea(entity);

            // Assert
            Assert.IsFalse(globalWorld.Has<CameraModeAreaLock>(cameraEntity));
        }

        [Test]
        public void KeepTheNewestAreaPublishedWhileNested()
        {
            // Arrange
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Act
            system.OnEnteredCameraModeArea(otherEntity, OTHER_AREA_CRDT_ID, CameraMode.Free, AREA_SIZE);

            // Assert
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraModeAreaLock areaLock));
            Assert.AreEqual(OTHER_AREA_CRDT_ID, areaLock.CrdtEntityId);
            Assert.AreEqual(CameraMode.Free, areaLock.Mode);
            Assert.AreEqual(2, areaLock.ActiveAreaCount);

            // Act — leaving the newer area hands the mirror back to the older one
            system.OnExitedCameraModeArea(otherEntity);

            // Assert
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out areaLock));
            Assert.AreEqual(AREA_CRDT_ID, areaLock.CrdtEntityId);
            Assert.AreEqual(1, areaLock.ActiveAreaCount);

            // Act
            system.OnExitedCameraModeArea(entity);

            // Assert
            Assert.IsFalse(globalWorld.Has<CameraModeAreaLock>(cameraEntity));
        }

        [Test]
        public void IgnoreASecondEnterOfTheSameArea()
        {
            // Arrange
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Act
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Assert
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            Assert.AreEqual(1, cameraComponent.CameraInputLocks);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraModeAreaLock areaLock));
            Assert.AreEqual(1, areaLock.ActiveAreaCount);
        }

        [Test]
        public void RemoveTheLockOnComponentRemoval()
        {
            // Arrange
            world.Add(entity, CreateComponent());
            system.Update(1);
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Act
            world.Remove<PBCameraModeArea>(entity);
            system.Update(1);

            // Assert
            Assert.IsFalse(globalWorld.Has<CameraModeAreaLock>(cameraEntity));
        }

        [Test]
        public void RemoveTheLockOnEntityDestruction()
        {
            // Arrange
            world.Add(entity, CreateComponent());
            system.Update(1);
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);

            // Act
            world.Add<DeleteEntityIntention>(entity);
            system.Update(1);

            // Assert
            Assert.IsFalse(globalWorld.Has<CameraModeAreaLock>(cameraEntity));
        }

        [Test]
        public void IgnoreDestructionOfAnAreaNeverEntered()
        {
            // Arrange — the player stands in one area; a second, never-entered area is destroyed
            system.OnEnteredCameraModeArea(entity, AREA_CRDT_ID, CameraMode.FirstPerson, AREA_SIZE);
            world.Add(otherEntity, CreateComponent());
            system.Update(1);

            // Act
            world.Add<DeleteEntityIntention>(otherEntity);
            system.Update(1);

            // Assert — the lock held by the entered area is untouched
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraComponent cameraComponent));
            Assert.AreEqual(1, cameraComponent.CameraInputLocks);
            Assert.AreEqual(CameraMode.FirstPerson, cameraComponent.Mode);
            Assert.IsTrue(globalWorld.TryGet(cameraEntity, out CameraModeAreaLock areaLock));
            Assert.AreEqual(AREA_CRDT_ID, areaLock.CrdtEntityId);
        }

        private static PBCameraModeArea CreateComponent() =>
            new ()
            {
                Area = new Vector3
                {
                    X = 2.5f,
                    Y = 3.8f,
                    Z = 8.5f,
                },
                IsDirty = true,
            };
    }
}
