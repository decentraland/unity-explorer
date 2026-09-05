using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class ApplyExternalImpulseShould
    {
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

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse launching a grounded character restores jumps");
            Assert.AreEqual(float.MinValue, jumpState.AirJumpDelay, "A pending air jump is cancelled by the launch");
            Assert.IsFalse(rigidTransform.IsGrounded, "The launch ungrounds the character");
        }

        [Test]
        public void ResetJumpCountOnUpwardImpulseNearGround()
        {
            // The scene may fire the pad before physics registers a grounded tick, while the character is still
            // within centimeters of the ground.
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 0.3f,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse close to the ground counts as a landing");
        }

        [Test]
        public void KeepJumpCountOnUpwardImpulseAwayFromGround()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 10f,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(2, jumpState.JumpCount, "An upward impulse clear of the ground is not a landing and does not restore jumps");
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

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(0f, rigidTransform.GravityVelocity.y, "A downward fall must not fight the launch impulse");
        }

        [Test]
        public void ApplyImpulseToExternalVelocityScaledByMass()
        {
            settings.CharacterMass.Returns(2f);

            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = true,
                ExternalImpulse = new Vector3(0f, 10f, 0f),
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(5f, rigidTransform.ExternalVelocity.y, "Δv = J / m, so a mass of 2 halves the impulse velocity");
            Assert.AreEqual(Vector3.zero, rigidTransform.ExternalImpulse, "The impulse is consumed after being applied");
        }

        [Test]
        public void KeepStateOnDownwardImpulse()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = true,
                ExternalImpulse = new Vector3(0f, -10f, 0f),
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(-10f, rigidTransform.ExternalVelocity.y, "A downward impulse is still applied to the velocity");
            Assert.AreEqual(2, jumpState.JumpCount, "A downward impulse is not a launch and does not restore jumps");
            Assert.IsTrue(rigidTransform.IsGrounded, "A downward impulse does not unground the character");
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

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(2, jumpState.JumpCount, "Only an upward impulse counts as a launch");
            Assert.IsTrue(rigidTransform.IsGrounded, "A horizontal impulse does not unground the character");
        }

        [Test]
        public void DoNothingOnNegligibleImpulse()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                ExternalImpulse = Vector3.zero,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(2, jumpState.JumpCount, "A negligible impulse leaves the jump state untouched");
            Assert.AreEqual(Vector3.zero, rigidTransform.ExternalVelocity, "A negligible impulse adds no velocity");
        }
    }
}
