using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterMotion.Tests
{
    public class MovePlayerWithDurationSystemShould : UnitySystemTestBase<MovePlayerWithDurationSystem>
    {
        private GameObject characterGameObject;
        private IAvatarView avatarView;

        [SetUp]
        public void Setup()
        {
            system = new MovePlayerWithDurationSystem(world);
            characterGameObject = new GameObject("TestCharacter");
            avatarView = Substitute.For<IAvatarView>();
        }

        [TearDown]
        public void CleanUp()
        {
            Object.DestroyImmediate(characterGameObject);
        }

        private Entity CreatePlayerEntity(
            Vector3 startPosition,
            Vector3 targetPosition,
            float duration,
            Vector3? cameraTarget = null,
            Vector3? avatarTarget = null)
        {
            characterGameObject.transform.position = startPosition;

            var characterTransform = new CharacterTransform(characterGameObject.transform);
            var rigidTransform = new CharacterRigidTransform();
            var animationComponent = new CharacterAnimationComponent();
            var movementInput = new MovementInputComponent { Kind = MovementKind.IDLE, Axes = Vector2.zero };
            var jumpInput = new JumpInputComponent { IsPressed = false };
            var moveIntent = new PlayerMoveToWithDurationIntent(startPosition, targetPosition, cameraTarget, avatarTarget, duration);

            return world.Create(
                characterTransform,
                rigidTransform,
                animationComponent,
                movementInput,
                jumpInput,
                moveIntent,
                avatarView
            );
        }

        [Test]
        public void InterpolatePositionOverTime()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // After half the duration, position should be roughly in the middle (with smooth step easing)
            system.Update(0.5f);

            Vector3 currentPosition = characterGameObject.transform.position;

            Assert.That(currentPosition.x, Is.GreaterThan(0f), "Position should have moved from start");
            Assert.That(currentPosition.x, Is.LessThan(10f), "Position should not have reached target yet");
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.True, "Intent should still be present");
        }

        [Test]
        public void ReachExactTargetPositionWhenComplete()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 5, 10);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // Complete the movement
            system.Update(1f);

            Assert.That(characterGameObject.transform.position, Is.EqualTo(targetPosition));
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.False, "Intent should be removed after completion");
        }

        [Test]
        public void AddMovePlayerToInfoWhenComplete()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 0.5f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(0.5f);

            Assert.That(world.Has<MovePlayerToInfo>(e), Is.True, "MovePlayerToInfo should be added after completion");
        }

        [Test]
        public void SetLookDirectionTowardsTarget()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(0.1f);

            var rigidTransform = world.Get<CharacterRigidTransform>(e);

            // Expected direction is normalized (1, 0, 0)
            Assert.That(rigidTransform.LookDirection.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rigidTransform.LookDirection.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ApplyFinalRotationTowardsAvatarTarget()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            Vector3 avatarTarget = new Vector3(10, 0, 10); // Look towards +Z from target
            float duration = 0.5f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration, avatarTarget: avatarTarget);

            // Complete the movement
            system.Update(0.5f);

            var rigidTransform = world.Get<CharacterRigidTransform>(e);

            // After completion, should face avatar target direction (0, 0, 1)
            Assert.That(rigidTransform.LookDirection.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(rigidTransform.LookDirection.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NotChangeFinalRotationWhenNoAvatarTarget()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 0.5f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration, avatarTarget: null);

            // Complete the movement
            system.Update(0.5f);

            var rigidTransform = world.Get<CharacterRigidTransform>(e);

            // Should still face movement direction (1, 0, 0) since no avatar target
            Assert.That(rigidTransform.LookDirection.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rigidTransform.LookDirection.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void InterruptMovementOnMovementInput()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 2f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // First update to start movement
            system.Update(0.1f);
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.True);

            // Simulate movement input
            world.Set(e, new MovementInputComponent { Kind = MovementKind.JOG, Axes = new Vector2(1, 0) });

            // Update should detect input and remove intent
            system.Update(0.1f);

            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.False, "Intent should be removed on movement input");
        }

        [Test]
        public void InterruptMovementOnJumpInput()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 2f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // First update to start movement
            system.Update(0.1f);
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.True);

            // Simulate jump input
            world.Set(e, new JumpInputComponent { IsPressed = true });

            // Update should detect jump and remove intent
            system.Update(0.1f);

            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.False, "Intent should be removed on jump input");
        }

        [Test]
        public void NotInterruptMovementOnIdleInput()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 2f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // Update with idle input (default)
            system.Update(0.5f);

            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.True, "Intent should remain with idle input");
        }

        [Test]
        public void UpdateAnimationComponentDuringMovement()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(0.5f);

            var animationComponent = world.Get<CharacterAnimationComponent>(e);

            Assert.That(animationComponent.States.IsGrounded, Is.True, "Should be grounded during movement");
            Assert.That(animationComponent.States.IsJumping, Is.False, "Should not be jumping");
            Assert.That(animationComponent.States.IsFalling, Is.False, "Should not be falling");
            Assert.That(animationComponent.IsSliding, Is.False, "Should not be sliding");
        }

        [Test]
        public void ResetAnimationToIdleOnCompletion()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 0.5f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // Complete the movement
            system.Update(0.5f);

            var animationComponent = world.Get<CharacterAnimationComponent>(e);

            Assert.That(animationComponent.States.MovementBlendValue, Is.EqualTo(0f), "Movement blend should be reset to idle");
            Assert.That(animationComponent.States.IsGrounded, Is.True, "Should remain grounded");
        }

        [Test]
        public void HandleZeroDurationAsInstant()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 0f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // Even with minimal delta time, should complete instantly
            system.Update(0.001f);

            Assert.That(characterGameObject.transform.position, Is.EqualTo(targetPosition));
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.False);
        }

        [Test]
        public void ApplySmoothStepEasing()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            // At t=0.5, smooth step should give 0.5 (since smooth step of 0.5 = 0.5)
            system.Update(0.5f);

            float expectedX = 5f; // Linear midpoint with smooth step at t=0.5
            Assert.That(characterGameObject.transform.position.x, Is.EqualTo(expectedX).Within(0.1f));
        }

        [Test]
        public void HandleDiagonalMovement()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 5, 10);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(1f);

            Assert.That(characterGameObject.transform.position, Is.EqualTo(targetPosition));

            var rigidTransform = world.Get<CharacterRigidTransform>(e);

            // Look direction should be in XZ plane only (Y=0)
            Vector3 expectedDir = new Vector3(10, 0, 10).normalized;
            Assert.That(rigidTransform.LookDirection.x, Is.EqualTo(expectedDir.x).Within(0.001f));
            Assert.That(rigidTransform.LookDirection.z, Is.EqualTo(expectedDir.z).Within(0.001f));
        }

        [Test]
        public void HandleVeryShortDuration()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(100, 0, 0);
            float duration = 0.01f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(0.01f);

            Assert.That(characterGameObject.transform.position, Is.EqualTo(targetPosition));
            Assert.That(world.Has<PlayerMoveToWithDurationIntent>(e), Is.False);
        }

        [Test]
        public void TrackLastFramePositionForAnimationSpeed()
        {
            Vector3 startPosition = Vector3.zero;
            Vector3 targetPosition = new Vector3(10, 0, 0);
            float duration = 1f;

            Entity e = CreatePlayerEntity(startPosition, targetPosition, duration);

            system.Update(0.1f);
            Vector3 positionAfterFirstFrame = characterGameObject.transform.position;

            var moveIntent = world.Get<PlayerMoveToWithDurationIntent>(e);
            Assert.That(moveIntent.LastFramePosition, Is.EqualTo(positionAfterFirstFrame));
        }
    }
}

