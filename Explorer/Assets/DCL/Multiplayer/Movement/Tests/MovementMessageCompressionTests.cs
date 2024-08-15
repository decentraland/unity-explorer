using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.Systems;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class MovementMessageCompressionTests
    {
        [TestCase(0f, 0f)]
        [TestCase(5f, 0.751f)]
        [TestCase(-5f, -1000f)]
        // We don't pass blend values with the message, so they should be reset to 0 on decompress
        public void ShouldResetBlendsToZeroOnDecompress(float moveBlend, float slideBlend)
        {
            // ARRANGE
            var originalMessage = new NetworkMovementMessage
            {
                animState = new AnimationStates
                {
                    MovementBlendValue = moveBlend,
                    SlideBlendValue = slideBlend,
                },
            };

            // ACT
            var decompressedMessage = originalMessage.Compress().Decompress();

            // ASSERT
            Assert.That(decompressedMessage.animState.MovementBlendValue, Is.EqualTo(0f));
            Assert.That(decompressedMessage.animState.SlideBlendValue, Is.EqualTo(0f));
        }

        [TestCase(MovementKind.JOG, false, false, false, false, false, false, false)]
        [TestCase(MovementKind.RUN, true, true, true, true, true, true, true)]
        [TestCase(MovementKind.WALK,  true, false, true, false, true, false, false)]
        [TestCase(MovementKind.IDLE, false, true, true, false, false, true, true)]
        public void ShouldCorrectlyEncodeAndDecodeAnimations(MovementKind movementKind, bool isSliding, bool isStunned, bool isGrounded, bool isJumping, bool isLongJump, bool isLongFall, bool isFalling)
        {
            // ARRANGE
            var originalMessage = new NetworkMovementMessage
            {
                movementKind = movementKind,
                isSliding = isSliding,
                isStunned = isStunned,
                animState = new AnimationStates
                {
                    IsGrounded = isGrounded,
                    IsJumping = isJumping,
                    IsLongJump = isLongJump,
                    IsLongFall = isLongFall,
                    IsFalling = isFalling,
                },
            };

            // ACT
            var decompressedMessage = originalMessage.Compress().Decompress();

            // ASSERT
            Assert.AreEqual(originalMessage.isStunned, decompressedMessage.isStunned);

            Assert.AreEqual(originalMessage.movementKind, decompressedMessage.movementKind);
            Assert.AreEqual(originalMessage.isSliding, decompressedMessage.isSliding);

            Assert.AreEqual(originalMessage.animState.IsGrounded, decompressedMessage.animState.IsGrounded);
            Assert.AreEqual(originalMessage.animState.IsJumping, decompressedMessage.animState.IsJumping);
            Assert.AreEqual(originalMessage.animState.IsLongJump, decompressedMessage.animState.IsLongJump);
            Assert.AreEqual(originalMessage.animState.IsLongFall, decompressedMessage.animState.IsLongFall);
            Assert.AreEqual(originalMessage.animState.IsFalling, decompressedMessage.animState.IsFalling);
        }


        [Test]
        [TestCase(0.001f, 0.751f, 0.001f )]
        [TestCase(8.0f, 4.521f, 8.356f )]
        [TestCase(15.999f, 17.25f, 15.999f )]
        [TestCase(16.5f, 150.05f, 16.3f)]
        [TestCase(-1.2f, -5.751f, -5.5f )]
        public void CompressAndDecompress_Position_ShouldReturnOriginalValue(float x, float y, float z)
        {
            // Arrange
            var originalMessage = new NetworkMovementMessage
            {
                timestamp = 123.456f,
                position = new Vector3(x, y, z),
                velocity = new Vector3(1f, 0f, 1f),
                animState = new AnimationStates(),
                isStunned = false,
            };

            // Act
            var compressedMessage = originalMessage.Compress();
            var decompressedMessage = compressedMessage.Decompress();

            // Assert
            Debug.Log($"VVV {x} - {decompressedMessage.position.x}  |  "
                      + $"{y} - {decompressedMessage.position.y} | "
                      + $"{z} - {decompressedMessage.position.z}");
        }

        [Test]
        [TestCase(0f, 0f, 0f )]
        [TestCase(1.153f, 2.753f, 3.523f)]
        [TestCase(-1.153f, -2.753f, -3.523f)]
        [TestCase(5.153f, 10f, 15.523f)]
        [TestCase(-5.153f, -10f, -15.523f)]
        [TestCase(20.241f, 30f, 49.523f)]
        [TestCase(-20.241f, -30f, -49.523f)]
        public void CompressAndDecompress_Velocity_ShouldReturnOriginalValue(float x, float y, float z)
        {
            // Arrange
            var originalMessage = new NetworkMovementMessage
            {
                timestamp = 123.456f,
                position = new Vector3(0, 0, 0),
                velocity = new Vector3(x, y, z),
                animState = new AnimationStates(),
                isStunned = false,
            };

            // Act
            var compressedMessage = originalMessage.Compress();
            var decompressedMessage = compressedMessage.Decompress();

            // Assert
            Debug.Log($"VVV {x} - {decompressedMessage.velocity.x}  |  "
                      + $"{y} - {decompressedMessage.velocity.y} | "
                      + $"{z} - {decompressedMessage.velocity.z}");
        }
    }
}
