using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class ApplyExternalImpulseShould
    {
        private const int PHYSICS_TICK = 1000;
        private const float DT = 0.02f;

        private ICharacterControllerSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = Substitute.For<ICharacterControllerSettings>();
            settings.CharacterMass.Returns(1f);
        }

        [Test]
        public void ResetJumpCountOnUpwardImpulseWhileGrounded()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = true,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2, AirJumpDelay = 0.1f };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse launching a grounded character restores jumps");
            Assert.AreEqual(float.MinValue, jumpState.AirJumpDelay, "A pending air jump is cancelled by the launch");
            Assert.IsFalse(rigidTransform.IsGrounded, "The launch ungrounds the character");
        }

        [Test]
        public void ResetJumpCountOnUpwardImpulseWhileDescending()
        {
            // Trigger-based bounce pads (e.g. mushrooms) launch the falling player without any physical ground
            // contact: IsGrounded stays false and the ground is meters below, yet the bounce must restore jumps.
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 10f,
                GravityVelocity = new Vector3(0f, -20f, 0f),
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse reversing a descent is a bounce and restores jumps");
        }

        [Test]
        public void ThrottleDescendingResetToCooldown()
        {
            var jumpState = new JumpState { JumpCount = 2 };

            CharacterRigidTransform Descending() =>
                new ()
                {
                    IsGrounded = false,
                    GroundDistance = 10f,
                    GravityVelocity = new Vector3(0f, -20f, 0f),
                    ExternalImpulse = Vector3.up * 10f,
                };

            // First descending impulse resets and records the tick
            var first = Descending();
            ApplyExternalImpulse.Execute(settings, ref first, ref jumpState, PHYSICS_TICK, DT);
            Assert.AreEqual(0, jumpState.JumpCount, "First descending bounce restores jumps");

            // Spend the jumps again, then a second impulse well within the cooldown must NOT reset
            jumpState.JumpCount = 2;
            var withinCooldown = Descending();
            ApplyExternalImpulse.Execute(settings, ref withinCooldown, ref jumpState, PHYSICS_TICK + 10, DT); // +0.2s
            Assert.AreEqual(2, jumpState.JumpCount, "A descending bounce within the cooldown is throttled");

            // After the cooldown elapses, a descending impulse resets again
            jumpState.JumpCount = 2;
            var afterCooldown = Descending();
            ApplyExternalImpulse.Execute(settings, ref afterCooldown, ref jumpState, PHYSICS_TICK + 60, DT); // +1.2s
            Assert.AreEqual(0, jumpState.JumpCount, "A descending bounce past the cooldown restores jumps again");
        }

        [Test]
        public void ResetJumpCountOnUpwardImpulseNearGroundIgnoringCooldown()
        {
            // The scene may fire the pad while rising but still within centimeters of the ground, before
            // physics registers a grounded tick. Ground-based resets are not throttled.
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 0.3f,
                GravityVelocity = new Vector3(0f, 5f, 0f),
                ExternalImpulse = Vector3.up * 10f,
            };

            // A recent descending reset must not block a genuine near-ground launch
            var jumpState = new JumpState { JumpCount = 2, LastDescendingResetTick = PHYSICS_TICK };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse close to the ground counts as a landing regardless of cooldown");
        }

        [Test]
        public void KeepJumpCountOnUpwardImpulseWhileRisingClearOfGround()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 10f,
                GravityVelocity = new Vector3(0f, 15f, 0f),
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(2, jumpState.JumpCount, "An impulse boosting an already rising character clear of the ground is not a landing");
        }

        [Test]
        public void ZeroFallingVelocityOnUpwardImpulse()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 10f,
                ExternalImpulse = Vector3.up * 10f,
                GravityVelocity = new Vector3(0f, -20f, 0f),
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(0f, rigidTransform.GravityVelocity.y, "A downward fall must not fight the launch impulse");
        }

        [Test]
        public void KeepJumpCountOnHorizontalImpulse()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = true,
                ExternalImpulse = Vector3.right * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState, PHYSICS_TICK, DT);

            Assert.AreEqual(2, jumpState.JumpCount, "Only an upward impulse counts as a launch");
            Assert.IsTrue(rigidTransform.IsGrounded, "A horizontal impulse does not unground the character");
        }
    }
}
