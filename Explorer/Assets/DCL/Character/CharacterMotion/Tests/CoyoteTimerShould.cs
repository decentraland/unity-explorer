using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.CharacterMotion.Tests
{
    [TestFixture]
    public class CoyoteTimerShould
    {
        private const int BONUS_FRAMES = 3;
        private ICharacterControllerSettings settings;
        private CharacterRigidTransform characterRigidTransform;
        private JumpInputComponent jumpInputComponent;
        private MovementInputComponent movementInputComponent;

        [SetUp]
        public void SetUp()
        {
            settings = Substitute.For<ICharacterControllerSettings>();
            settings.JumpGraceTime.Returns(UnityEngine.Time.fixedDeltaTime * BONUS_FRAMES);
            settings.Gravity.Returns(-10);
            settings.JumpGravityFactor.Returns(2);
            settings.JogJumpHeight.Returns(1);
            settings.RunSpeed.Returns(10);
        }

        // Coyote Timer: Pressing Jump before touching ground
        [Test]
        public void JumpWhenTriggeredBeforeGrounding()
        {
            SetupFallingPlayer();
            var physicsTick = 10;
            SetupJumpFrameAt(8);

            // We check that we are not jumping before being grounded
            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick);

            Assert.IsFalse(characterRigidTransform.IsGrounded, "Is Grounded");
            Assert.IsTrue(characterRigidTransform.GravityVelocity.y < 0, "Is Falling");

            characterRigidTransform.IsGrounded = true;

            // At this frame we get grounded, and the jump is triggered thanks to the bonus frames
            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick + 1);

            Assert.AreEqual(physicsTick + 1, characterRigidTransform.LastJumpFrame, "Jump Frame");
            Assert.IsTrue(characterRigidTransform.GravityVelocity.y > 0, "Is Jumping");
        }

        // Coyote Timer: Pressing Jump before touching ground
        [Test]
        public void NotJumpWhenTriggeredBeforeGroundingTooEarly()
        {
            SetupFallingPlayer();
            var physicsTick = 10;
            SetupJumpFrameAt(3);

            // We get grounded at frame 10, the bonus frames were not enough to make us jump
            characterRigidTransform.IsGrounded = true;
            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick);

            Assert.IsTrue(characterRigidTransform.IsGrounded, "Is Grounded");
        }

        // Coyote Timer: Pressing Jump after being ungrounded
        [Test]
        public void JumpWhenTriggeredAfterFallingEarly()
        {
            SetupFallingPlayer();
            var physicsTick = 10;
            SetupJumpFrameAt(physicsTick);

            // Setup the last grounded frame to be inside the bonus frames range
            characterRigidTransform.LastGroundedFrame = physicsTick - BONUS_FRAMES + 1;
            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick);

            Assert.AreEqual(physicsTick, characterRigidTransform.LastJumpFrame, "Jump Frame");
            Assert.IsTrue(characterRigidTransform.GravityVelocity.y > 0, "Is Jumping");
        }

        // Coyote Timer: Pressing Jump after being ungrounded
        [Test]
        public void NotJumpWhenTriggeredAfterFallingLate()
        {
            SetupFallingPlayer();
            var physicsTick = 10;
            SetupJumpFrameAt(9);

            // Setup the last grounded frame to be outside the bonus frames range
            characterRigidTransform.LastGroundedFrame = physicsTick - BONUS_FRAMES - 1;
            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick);

            Assert.IsFalse(characterRigidTransform.GravityVelocity.y > 0, "Is Jumping");
        }

        // Coyote Timer: Pressing Jump after being ungrounded
        // Avoid Double Jumping
        [Test]
        public void NotJumpWhenTriggeredAfterJumpingInsideBonusFrames()
        {
            SetupFallingPlayer();
            var physicsTick = 10;
            SetupJumpFrameAt(physicsTick);

            characterRigidTransform.GravityVelocity = Vector3.up;

            // Setup the last grounded frame to be inside the bonus frames range
            characterRigidTransform.LastGroundedFrame = physicsTick - BONUS_FRAMES + 1;

            // Setup LastJumpFrame to be the last frame
            characterRigidTransform.LastJumpFrame = characterRigidTransform.LastGroundedFrame - 1;

            ApplyJump.Execute(settings, ref characterRigidTransform, ref jumpInputComponent, in movementInputComponent, physicsTick);

            Assert.AreEqual(characterRigidTransform.LastGroundedFrame - 1, characterRigidTransform.LastJumpFrame, "Jump Frame");
            Assert.IsTrue(characterRigidTransform.GravityVelocity.magnitude < 1.5f, "Velocity didn't Change");
        }

        private void SetupJumpFrameAt(int frame)
        {
            jumpInputComponent.Trigger.TickWhenJumpOccurred = frame;
        }

        private void SetupFallingPlayer()
        {
            characterRigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GravityVelocity = new Vector3(0, -10, 0),
            };

            jumpInputComponent = new JumpInputComponent();
            movementInputComponent = new MovementInputComponent();
        }
    }
}
