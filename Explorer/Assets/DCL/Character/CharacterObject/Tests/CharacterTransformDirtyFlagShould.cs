using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.ECSComponents;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.Systems;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Character.Tests
{
    public class CharacterTransformDirtyFlagShould : UnitySystemTestBase<PartitionGlobalAssetEntitiesSystem>
    {
        private const float MINIMAL_DISTANCE_DIFFERENCE = 0.01f;
        private const float BELLOW_MINIMAL_DISTANCE_DIFFERENCE = 0.009f;
        private const float ABOVE_MINIMAL_DISTANCE_DIFFERENCE = 0.011f;

        private World globalWorld;
        
        private IReadOnlyCameraSamplingData cameraSamplingData;
        private IPartitionSettings partitionSettings;
        private IComponentPool<PartitionComponent> partitionComponentPool;
        private GameObject testGameObject;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            
            // Default camera state - not dirty to avoid interference with repartitioning
            cameraSamplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            cameraSamplingData.Position.Returns(Vector3.zero);
            cameraSamplingData.Forward.Returns(Vector3.forward);
            cameraSamplingData.IsDirty.Returns(false);
            
            partitionSettings = Substitute.For<IPartitionSettings>();
            
            partitionComponentPool = Substitute.For<IComponentPool<PartitionComponent>>();
            partitionComponentPool.Get().Returns(new PartitionComponent());
            
            system = new PartitionGlobalAssetEntitiesSystem(
                globalWorld, 
                partitionComponentPool, 
                partitionSettings, 
                cameraSamplingData
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
            
            globalWorld.Dispose();
        }

        [Test]
        public void SetDirtyWhenPositionChangesAboveThreshold()
        {
            // Arrange
            testGameObject = new GameObject("TestEntity");
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Assert.IsFalse(characterTransform.IsDirty, "IsDirty should be false initially");
            
            // Act - Move above threshold (0.01f)
            Vector3 newPosition = testGameObject.transform.position + Vector3.one * ABOVE_MINIMAL_DISTANCE_DIFFERENCE;
            characterTransform.SetPositionWithDirtyCheck(newPosition);
            
            // Assert
            Assert.IsTrue(characterTransform.IsDirty, 
                $"IsDirty should be true when position changes by more than {MINIMAL_DISTANCE_DIFFERENCE}");
            Assert.AreEqual(newPosition, characterTransform.Position);
        }

        [Test]
        public void NotSetDirtyWhenPositionChangeBelowThreshold()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Assert.IsFalse(characterTransform.IsDirty, "IsDirty should be false initially");
            
            // Act - Move below threshold
            Vector3 newPosition = testGameObject.transform.position + Vector3.one * (BELLOW_MINIMAL_DISTANCE_DIFFERENCE / 3);
            characterTransform.SetPositionWithDirtyCheck(newPosition);
            
            // Assert
            Assert.IsFalse(characterTransform.IsDirty, 
                $"IsDirty should remain false when position changes by less than {MINIMAL_DISTANCE_DIFFERENCE}");
        }

        [Test]
        public void SetDirtyWithSetPositionAndRotation()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            // Act
            Vector3 newPosition = testGameObject.transform.position + Vector3.forward * ABOVE_MINIMAL_DISTANCE_DIFFERENCE;
            Quaternion newRotation = Quaternion.Euler(0, 90, 0);
            characterTransform.SetPositionAndRotationWithDirtyCheck(newPosition, newRotation);
            
            // Assert
            Assert.IsTrue(characterTransform.IsDirty, 
                "IsDirty should be true after SetPositionAndRotationWithDirtyCheck");
            Assert.AreEqual(newPosition, characterTransform.Position);
            Assert.AreEqual(newRotation.eulerAngles, characterTransform.Rotation.eulerAngles);
        }

        [Test]
        public void ClearDirtyResetsFlag()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            // Set dirty first
            characterTransform.SetPositionWithDirtyCheck(Vector3.one);
            Assert.IsTrue(characterTransform.IsDirty);
            
            // Act
            characterTransform.ClearDirty();
            
            // Assert
            Assert.IsFalse(characterTransform.IsDirty, "ClearDirty should reset the flag");
        }

        [Test]
        public void ClearDirtyForAvatarShapeEntities()
        {
            // Arrange
            testGameObject = new GameObject("AvatarObject");
            testGameObject.transform.position = new Vector3(10, 0, 10);
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Entity avatarEntity = globalWorld.Create(
                characterTransform,
                new PBAvatarShape(),
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );
            
            // Make transform dirty
            Vector3 newPosition = testGameObject.transform.position + Vector3.one * ABOVE_MINIMAL_DISTANCE_DIFFERENCE;
            characterTransform.SetPositionWithDirtyCheck(newPosition);
            globalWorld.Set(avatarEntity, characterTransform);
            
            // Verify it's dirty
            ref CharacterTransform dirtyTransform = ref globalWorld.Get<CharacterTransform>(avatarEntity);
            Assert.IsTrue(dirtyTransform.IsDirty, "Transform should be dirty before system update");
            
            // Act - Run partition system
            system.Update(0.1f);
            
            // Assert - Dirty flag should be cleared
            ref CharacterTransform clearedTransform = ref globalWorld.Get<CharacterTransform>(avatarEntity);
            Assert.IsFalse(clearedTransform.IsDirty, 
                "PartitionSystem should clear IsDirty for avatar entities");
        }

        [Test]
        public void OnlyProcessDirtyEntitiesWhenCameraNotDirty()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            var dirtyTransform = new CharacterTransform(testGameObject.transform);
            
            GameObject cleanGameObject = new GameObject("CleanObject");
            var cleanTransform = new CharacterTransform(cleanGameObject.transform);
            
            // Create two entities - one dirty, one clean
            Entity dirtyEntity = globalWorld.Create(
                dirtyTransform,
                new PBAvatarShape(),
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );
            
            Entity cleanEntity = globalWorld.Create(
                cleanTransform,
                new PBAvatarShape(), 
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );
            
            // Make only one transform dirty
            dirtyTransform.SetPositionWithDirtyCheck(Vector3.one * ABOVE_MINIMAL_DISTANCE_DIFFERENCE);
            globalWorld.Set(dirtyEntity, dirtyTransform);
            
            // Verify states
            ref CharacterTransform dirtyCheck = ref globalWorld.Get<CharacterTransform>(dirtyEntity);
            ref CharacterTransform cleanCheck = ref globalWorld.Get<CharacterTransform>(cleanEntity);
            Assert.IsTrue(dirtyCheck.IsDirty, "First entity should be dirty");
            Assert.IsFalse(cleanCheck.IsDirty, "Second entity should be clean");
            
            // Act - Update with camera not dirty (should only process dirty entities)
            cameraSamplingData.IsDirty.Returns(false);
            system.Update(0.1f);
            
            // Assert
            ref CharacterTransform dirtyAfter = ref globalWorld.Get<CharacterTransform>(dirtyEntity);
            ref CharacterTransform cleanAfter = ref globalWorld.Get<CharacterTransform>(cleanEntity);
            
            Assert.IsFalse(dirtyAfter.IsDirty, "Dirty entity should be cleared");
            Assert.IsFalse(cleanAfter.IsDirty, "Clean entity should remain clean");
            
            // Cleanup
            Object.DestroyImmediate(cleanGameObject);
        }

        [Test]
        public void RepartitionAllWhenCameraIsDirtyClearsTransformFlag()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Entity entity = globalWorld.Create(
                characterTransform,
                new PBAvatarShape(),
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );
            
            // Entity starts clean
            ref CharacterTransform initialTransform = ref globalWorld.Get<CharacterTransform>(entity);
            Assert.IsFalse(initialTransform.IsDirty, "Transform should start clean");
            
            // Act - Update with camera dirty (triggers RePartitionExistingEntityQuery)
            cameraSamplingData.IsDirty.Returns(true);
            system.Update(0.1f);
            
            // Assert - CharacterTransform.IsDirty should remain unchanged since entity didn't move
            ref CharacterTransform afterTransform = ref globalWorld.Get<CharacterTransform>(entity);
            Assert.IsFalse(afterTransform.IsDirty, 
                "CharacterTransform.IsDirty shouldn't change when camera moves");
        }

        [Test]
        public void UseCheapDistanceCalculation()
        {
            // Arrange
            testGameObject = new GameObject("TestObject");
            testGameObject.transform.position = Vector3.zero;
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Vector3 exactVector = new Vector3(MINIMAL_DISTANCE_DIFFERENCE * 0.4f, MINIMAL_DISTANCE_DIFFERENCE * 0.3f, 
                MINIMAL_DISTANCE_DIFFERENCE * 0.3f);
            Vector3 bellowMinVector = new Vector3(MINIMAL_DISTANCE_DIFFERENCE * 0.3f, MINIMAL_DISTANCE_DIFFERENCE * 0.3f, 
                MINIMAL_DISTANCE_DIFFERENCE * 0.3f);
            
            // Act & Assert - Test case 1: Each axis contributes to distance
            characterTransform.SetPositionWithDirtyCheck(exactVector);
            Assert.IsTrue(characterTransform.IsDirty, 
                "Should be dirty when Manhattan distance = 0.01f");
            
            // Reset
            characterTransform.ClearDirty();
            
            // Test case 2: Below threshold
            Vector3 movement2 = testGameObject.transform.position + bellowMinVector;
            characterTransform.SetPositionWithDirtyCheck(movement2);
            Assert.IsFalse(characterTransform.IsDirty, 
                "Should not be dirty when Manhattan distance < 0.01f");

            // This passes dirty check, setting up CharacterTransform.oldPosition, which we can later reset to default value.
            characterTransform.SetPositionWithDirtyCheck(testGameObject.transform.position + Vector3.one);
            characterTransform.SetPositionWithDirtyCheck(testGameObject.transform.position);
            characterTransform.ClearDirty();
            
            // Test case 3: Single axis movement
            Vector3 movement3 = testGameObject.transform.position + new Vector3(0.011f, 0, 0);
            characterTransform.SetPositionWithDirtyCheck(movement3);
            Assert.IsTrue(characterTransform.IsDirty, 
                "Should be dirty when single axis movement > 0.01f");
        }

        [Test]
        public void SetsDirtyCharacterTransformViaRemotePlayersMovementSystem()
        {
            // Arrange
            testGameObject = new GameObject("RemotePlayer");
            testGameObject.transform.position = Vector3.zero;
            var characterTransform = new CharacterTransform(testGameObject.transform);
            
            Entity remoteEntity = globalWorld.Create(
                characterTransform,
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );
            
            // Setup movement system dependencies
            var movementSettings = Substitute.For<IMultiplayerMovementSettings>();
            movementSettings.MoveSendRate.Returns(0.1f);
            movementSettings.MinTeleportDistance.Returns(100f);
            movementSettings.MinPositionDelta.Returns(0.01f);
            movementSettings.MinRotationDelta.Returns(0.01f);
            
            var interpolationSettings = new RemotePlayerInterpolationSettings
            {
                UseSpeedUp = false,
                UseBlend = false
            };
            movementSettings.InterpolationSettings.Returns(interpolationSettings);
            
            var characterControllerSettings = Substitute.For<ICharacterControllerSettings>();
            characterControllerSettings.RotationSpeed.Returns(10f);
            
            var movementSystem = new RemotePlayersMovementSystem(
                globalWorld, movementSettings, characterControllerSettings);
            
            // Setup movement component
            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: queue => queue.Clear()
            );
            
            globalWorld.Add(remoteEntity, new RemotePlayerMovementComponent(queuePoolFullMovementMessage));
            globalWorld.Add(remoteEntity, new InterpolationComponent());
            globalWorld.Add(remoteEntity, new ExtrapolationComponent());
            
            ref RemotePlayerMovementComponent movementComponent = ref globalWorld.Get<RemotePlayerMovementComponent>(remoteEntity);
            movementComponent.Initialized = false;
            movementComponent.InitialCooldownTime = 0.3f;
            globalWorld.Set(remoteEntity, movementComponent);
            
            // Verify clean state
            ref CharacterTransform beforeTransform = ref globalWorld.Get<CharacterTransform>(remoteEntity);
            Assert.IsFalse(beforeTransform.IsDirty, "Should start clean");
            
            // Act - Process first movement message
            var firstMessage = new NetworkMovementMessage
            {
                timestamp = 1f,
                position = new Vector3(1f, 0f, 1f),
                rotationY = 45f,
                velocity = Vector3.zero,
                velocitySqrMagnitude = 0f,
                movementKind = MovementKind.IDLE,
                isInstant = false
            };
            
            movementComponent.Enqueue(firstMessage);
            globalWorld.Set(remoteEntity, movementComponent);
            movementSystem.Update(0.1f);
            
            // Assert
            ref CharacterTransform afterTransform = ref globalWorld.Get<CharacterTransform>(remoteEntity);
            Assert.IsTrue(afterTransform.IsDirty, 
                "RemotePlayersMovementSystem should set IsDirty via CharacterTransform.SetPositionAndRotationWithDirtyCheck");
            Assert.AreEqual(firstMessage.position, afterTransform.Position, 
                "Position should be updated");
        }

        [Test]
        public void CompleteFlow_RemoteMovementSetsDirty_PartitionSystemClearsIt()
        {
            // Arrange - Create local partition system with camera dirty for RePartitionExistingEntity
            var localCameraSamplingData = Substitute.For<IReadOnlyCameraSamplingData>();
            localCameraSamplingData.Position.Returns(Vector3.zero);
            localCameraSamplingData.Forward.Returns(Vector3.forward);
            localCameraSamplingData.IsDirty.Returns(true);
            
            var localPartitionSystem = new PartitionGlobalAssetEntitiesSystem(
                globalWorld, 
                partitionComponentPool, 
                partitionSettings, 
                localCameraSamplingData
            );
            
            testGameObject = new GameObject("CompleteFlowTest");
            testGameObject.transform.position = Vector3.zero;
            var characterTransform = new CharacterTransform(testGameObject.transform);

            Entity entity = globalWorld.Create(
                characterTransform,
                Profile.Create("Ia4Ia5Cth0ulhu2Ftaghn2", "fake user", new DCL.Profiles.Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())), 
                new AvatarShapeComponent(),
                new PartitionComponent { Bucket = 0, IsBehind = false, IsDirty = false }
            );

            // Setup movement system
            var movementSettings = Substitute.For<IMultiplayerMovementSettings>();
            movementSettings.MoveSendRate.Returns(0.1f);
            movementSettings.MinTeleportDistance.Returns(100f);
            movementSettings.InterpolationSettings.Returns(new RemotePlayerInterpolationSettings());

            var characterControllerSettings = Substitute.For<ICharacterControllerSettings>();

            var movementSystem = new RemotePlayersMovementSystem(
                globalWorld, movementSettings, characterControllerSettings);

            var queuePoolFullMovementMessage = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(
                () => new SimplePriorityQueue<NetworkMovementMessage>(),
                actionOnRelease: queue => queue.Clear()
            );

            globalWorld.Add(entity, new RemotePlayerMovementComponent(queuePoolFullMovementMessage));
            globalWorld.Add(entity, new InterpolationComponent());
            globalWorld.Add(entity, new ExtrapolationComponent());

            ref RemotePlayerMovementComponent movementComponent = ref globalWorld.Get<RemotePlayerMovementComponent>(entity);
            movementComponent.Initialized = false;
            movementComponent.InitialCooldownTime = 0.3f;

            // Act & Assert
            // Step 1: Movement system sets dirty
            var message = new NetworkMovementMessage
            {
                timestamp = 1f,
                position = new Vector3(5f, 0f, 5f),
                rotationY = 90f,
                velocity = Vector3.zero,
                velocitySqrMagnitude = 0f,
                movementKind = MovementKind.IDLE,
                isInstant = false
            };

            movementComponent.Enqueue(message);
            globalWorld.Set(entity, movementComponent);

            movementSystem.Update(0.1f);

            ref CharacterTransform afterMovement = ref globalWorld.Get<CharacterTransform>(entity);
            Assert.IsTrue(afterMovement.IsDirty,
                "Movement system should set dirty flag");

            // Step 2: Partition system clears dirty
            localPartitionSystem.Update(0.1f);

            ref CharacterTransform afterPartition = ref globalWorld.Get<CharacterTransform>(entity);
            Assert.IsFalse(afterPartition.IsDirty,
                "Partition system should clear dirty flag");

            Assert.AreEqual(message.position, afterPartition.Position,
                "Position should be maintained through the flow");
            
            localPartitionSystem.Dispose();
        }
    }
}