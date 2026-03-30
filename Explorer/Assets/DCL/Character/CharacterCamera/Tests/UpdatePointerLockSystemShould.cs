using Arch.Core;
using DCL.CharacterCamera.Systems;
using DCL.ECSComponents;
using DCL.Input.Component;
using DCL.Utilities;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace DCL.CharacterCamera.Tests
{
    public class UpdatePointerLockSystemShould : UnitySystemTestBase<UpdatePointerLockSystem>
    {
        private World globalWorld;
        private IExposedCameraData cameraData;
        private Entity cameraEntity;
        private ISceneStateProvider sceneStateProvider;
        private Entity globalCameraEntity;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            globalCameraEntity = globalWorld.Create();

            cameraData = Substitute.For<IExposedCameraData>();
            // Setup CameraEntityProxy to point to globalCameraEntity
            var proxy = new ObjectProxy<Entity>();
            proxy.SetObject(globalCameraEntity);
            cameraData.CameraEntityProxy.Returns(proxy);

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            cameraEntity = world.Create();

            system = new UpdatePointerLockSystem(world, globalWorld, cameraData, cameraEntity, sceneStateProvider);
        }

        [TearDown]
        public void TearDown()
        {
            globalWorld.Dispose();
        }

        [Test]
        public void UpdateLockFromScene()
        {
            // Arrange
            var pbPointerLock = new PBPointerLock { IsPointerLocked = true, IsDirty = true };
            world.Add(cameraEntity, pbPointerLock);

            // Act
            system.Update(0);

            // Assert
            // Check if PointerLockIntention is added to globalCameraEntity
            Assert.IsTrue(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
            var intention = globalWorld.Get<PointerLockIntention>(globalCameraEntity);
            Assert.IsTrue(intention.Locked);

            // Check if PBPointerLock.IsDirty is false
            var updatedPbPointerLock = world.Get<PBPointerLock>(cameraEntity);
            Assert.IsFalse(updatedPbPointerLock.IsDirty);
        }

        [Test]
        public void UpdateUnlockFromScene()
        {
            // Arrange
            var pbPointerLock = new PBPointerLock { IsPointerLocked = false, IsDirty = true };
            world.Add(cameraEntity, pbPointerLock);

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
            var intention = globalWorld.Get<PointerLockIntention>(globalCameraEntity);
            Assert.IsFalse(intention.Locked);

            var updatedPbPointerLock = world.Get<PBPointerLock>(cameraEntity);
            Assert.IsFalse(updatedPbPointerLock.IsDirty);
        }

        [Test]
        public void OverwriteExistingIntention()
        {
            // Arrange
            // Existing intention with Locked = true
            globalWorld.Add(globalCameraEntity, new PointerLockIntention(true));

            // New intention with Locked = false
            var pbPointerLock = new PBPointerLock { IsPointerLocked = false, IsDirty = true };
            world.Add(cameraEntity, pbPointerLock);

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
            var intention = globalWorld.Get<PointerLockIntention>(globalCameraEntity);
            Assert.IsFalse(intention.Locked);
        }

        [Test]
        public void NotUpdateIfNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            var pbPointerLock = new PBPointerLock { IsPointerLocked = true, IsDirty = true };
            world.Add(cameraEntity, pbPointerLock);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<PointerLockIntention>(globalCameraEntity));

            // PBPointerLock should remain dirty
            var updatedPbPointerLock = world.Get<PBPointerLock>(cameraEntity);
            Assert.IsTrue(updatedPbPointerLock.IsDirty);
        }

        [Test]
        public void ProcessUpdateWhenSceneBecomesCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            var pbPointerLock = new PBPointerLock { IsPointerLocked = true, IsDirty = true };
            world.Add(cameraEntity, pbPointerLock);

            // Act: Update when not current
            system.Update(0);

            // Assert: No update yet
            Assert.IsFalse(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
            var updatedPbPointerLock = world.Get<PBPointerLock>(cameraEntity);
            Assert.IsTrue(updatedPbPointerLock.IsDirty);

            // Act: Scene becomes current
            sceneStateProvider.IsCurrent.Returns(true);
            system.Update(0);

            // Assert: Update processed
            Assert.IsTrue(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
            var intention = globalWorld.Get<PointerLockIntention>(globalCameraEntity);
            Assert.IsTrue(intention.Locked);

            // Check if PBPointerLock.IsDirty is false
            updatedPbPointerLock = world.Get<PBPointerLock>(cameraEntity);
            Assert.IsFalse(updatedPbPointerLock.IsDirty);
        }

        [Test]
        public void NotUpdateIfNotDirty()
        {
            // Arrange
            var pbPointerLock = new PBPointerLock { IsPointerLocked = true, IsDirty = false };
            world.Add(cameraEntity, pbPointerLock);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<PointerLockIntention>(globalCameraEntity));
        }
    }
}
