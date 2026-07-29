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
                GroundDistance = 0f,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2, AirJumpDelay = 0.1f };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(0, jumpState.JumpCount, "An upward impulse taken on the ground counts as a landing");
            Assert.AreEqual(float.MinValue, jumpState.AirJumpDelay, "A pending air jump is cancelled by the landing");
            Assert.IsFalse(rigidTransform.IsGrounded, "The launch ungrounds the character");
        }

        [Test]
        public void ResetJumpCountOnUpwardImpulseNearGround()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 0.3f,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(0, jumpState.JumpCount, "The scene may fire the jump pad before physics registers a grounded tick");
        }

        [Test]
        public void KeepJumpCountOnUpwardImpulseHighInTheAir()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = false,
                GroundDistance = 100f,
                ExternalImpulse = Vector3.up * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(2, jumpState.JumpCount, "Mid-air impulses (wind, cannons) must not grant extra air jumps");
        }

        [Test]
        public void KeepJumpCountOnHorizontalImpulse()
        {
            var rigidTransform = new CharacterRigidTransform
            {
                IsGrounded = true,
                GroundDistance = 0f,
                ExternalImpulse = Vector3.right * 10f,
            };

            var jumpState = new JumpState { JumpCount = 2 };

            ApplyExternalImpulse.Execute(settings, ref rigidTransform, ref jumpState);

            Assert.AreEqual(2, jumpState.JumpCount, "Only an upward impulse counts as a landing");
            Assert.IsTrue(rigidTransform.IsGrounded, "A horizontal impulse does not unground the character");
        }
    }
}
