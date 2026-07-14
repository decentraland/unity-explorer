using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class ApplyGlidingShould
    {
        private const float GLIDE_MAX_GRAVITY = 1f;

        private ICharacterControllerSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = Substitute.For<ICharacterControllerSettings>();
            settings.GlideMaxGravity.Returns(GLIDE_MAX_GRAVITY);
            settings.GlideMinGroundDistance.Returns(0.2f);
        }

        [Test]
        public void ClampFallSpeedWhileGliding()
        {
            var rigidTransform = GlidingRigidTransform();
            rigidTransform.GravityVelocity = new Vector3(0f, -30f, 0f);
            GlideState glideState = GlidingState();

            Execute(rigidTransform, ref glideState);

            Assert.IsTrue(Mathf.Approximately(-GLIDE_MAX_GRAVITY, rigidTransform.GravityVelocity.y), "Fall speed is clamped to GlideMaxGravity");
            Assert.AreEqual(GlideStateValue.GLIDING, glideState.Value);
        }

        [Test]
        public void PreserveUpwardVelocityWhileGliding()
        {
            var rigidTransform = GlidingRigidTransform();

            // Upward velocity accumulated from an external force (e.g. wind) or jump momentum
            rigidTransform.GravityVelocity = new Vector3(0f, 15f, 0f);
            GlideState glideState = GlidingState();

            Execute(rigidTransform, ref glideState);

            Assert.IsTrue(Mathf.Approximately(15f, rigidTransform.GravityVelocity.y), "Upward velocity is not clamped");
        }

        [Test]
        public void NotClampSlowFall()
        {
            var rigidTransform = GlidingRigidTransform();
            rigidTransform.GravityVelocity = new Vector3(0f, -0.5f, 0f);
            GlideState glideState = GlidingState();

            Execute(rigidTransform, ref glideState);

            Assert.IsTrue(Mathf.Approximately(-0.5f, rigidTransform.GravityVelocity.y), "Fall speed below the limit is untouched");
        }

        private void Execute(CharacterRigidTransform rigidTransform, ref GlideState glideState)
        {
            var jumpState = new JumpState { JumpCount = 2, MaxAirJumpCount = 1 };
            var jumpInput = new JumpInputComponent { IsPressed = true };

            ApplyGliding.Execute(settings, rigidTransform, jumpState, jumpInput, ref glideState, physicsTick: 100, dt: 0.02f);
        }

        private static CharacterRigidTransform GlidingRigidTransform() =>
            new ()
            {
                IsGrounded = false,
                GroundDistance = 100f,
            };

        private static GlideState GlidingState() =>
            new () { Value = GlideStateValue.GLIDING };
    }
}
