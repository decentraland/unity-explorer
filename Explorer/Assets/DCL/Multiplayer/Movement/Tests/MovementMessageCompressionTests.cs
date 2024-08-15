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
            NetworkMovementMessage decompressedMessage = originalMessage.Compress().Decompress();

            // ASSERT
            Assert.That(decompressedMessage.animState.MovementBlendValue, Is.EqualTo(0f));
            Assert.That(decompressedMessage.animState.SlideBlendValue, Is.EqualTo(0f));
        }

        [TestCase(MovementKind.JOG, false, false, false, false, false, false, false)]
        [TestCase(MovementKind.RUN, true, true, true, true, true, true, true)]
        [TestCase(MovementKind.WALK, true, false, true, false, true, false, false)]
        [TestCase(MovementKind.IDLE, false, true, true, false, false, true, true)]
        public void ShouldCorrectlyEncodeAndDecodeAnimations(MovementKind movementKind, bool isSliding, bool isStunned, bool isGrounded, bool isJumping,
            bool isLongJump, bool isLongFall, bool isFalling)
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
            NetworkMovementMessage decompressedMessage = originalMessage.Compress().Decompress();

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

        [TestCase(-1.2f, -5.5f)]
        [TestCase(-0.001f, -0.001f)]
        [TestCase(0, 0)]
        [TestCase(0.001f, 0.001f)]
        [TestCase(8.0f, 8.356f)]
        [TestCase(15.999f, 15.999f)]
        [TestCase(16, 16)]
        [TestCase(16.001f, 16.001f)]
        [TestCase(16.5f, 16.3f)]
        [TestCase(52.5f, -13.33f)]
        [TestCase(-70f, 116f)]
        [TestCase(-1000f, 1000f)]
        [TestCase(1000f, -1000f)]
        [TestCase(ParcelEncoder.MAX_X * ParcelEncoder.PARCEL_SIZE, ParcelEncoder.MAX_Y * ParcelEncoder.PARCEL_SIZE)]
        [TestCase(ParcelEncoder.MIN_X * ParcelEncoder.PARCEL_SIZE, ParcelEncoder.MIN_Y * ParcelEncoder.PARCEL_SIZE)]
        [TestCase((ParcelEncoder.MAX_X * ParcelEncoder.PARCEL_SIZE) - 0.001f, (ParcelEncoder.MAX_Y * ParcelEncoder.PARCEL_SIZE) - 0.001f)]
        [TestCase((ParcelEncoder.MIN_X * ParcelEncoder.PARCEL_SIZE) + 0.001f, (ParcelEncoder.MIN_Y * ParcelEncoder.PARCEL_SIZE) + 0.001f)]
        public void ShouldCorrectlyEncodeAndDecodeXZPositions(float x, float z)
        {
            // Arrange
            float stepSize = CompressionConfig.PARCEL_SIZE / Mathf.Pow(2, CompressionConfig.XZ_BITS);
            float quantizationError = (stepSize / 2f) + 0.0002f; // there is a small deviation at 8.0f point (less then 0.0002f)

            var originalMessage = new NetworkMovementMessage { position = new Vector3(x, 0f, z) };

            // Act
            NetworkMovementMessage decompressedMessage = originalMessage.Compress().Decompress();

            // Assert
            Assert.AreEqual(originalMessage.position.x, decompressedMessage.position.x, quantizationError);
            Assert.AreEqual(originalMessage.position.z, decompressedMessage.position.z, quantizationError);

            Debug.Log($" XZ quantization error = {quantizationError} | original: {originalMessage.position.x}, {originalMessage.position.z} | decompressed: {decompressedMessage.position.x}, {decompressedMessage.position.z}");
        }

        [TestCase(-5.751f)]
        [TestCase(-0.001f)]
        [TestCase(0f)]
        [TestCase(0.751f)]
        [TestCase(4.521f)]
        [TestCase(17.25f)]
        [TestCase(CompressionConfig.Y_MAX / 8)]
        [TestCase(CompressionConfig.Y_MAX / 4)]
        [TestCase(CompressionConfig.Y_MAX / 3)]
        [TestCase(CompressionConfig.Y_MAX / 2)]
        [TestCase(CompressionConfig.Y_MAX)]
        [TestCase(CompressionConfig.Y_MAX + 0.05f)]
        [TestCase(10 * CompressionConfig.Y_MAX)]
        public void ShouldCorrectlyEncodeAndDecodeYPositions(float y)
        {
            // Arrange
            float stepSize = CompressionConfig.Y_MAX / Mathf.Pow(2, CompressionConfig.Y_BITS);
            float quantizationError = stepSize / 2f;

            var originalMessage = new NetworkMovementMessage { position = new Vector3(0f, y, 0f) };

            // Act
            NetworkMovementMessage decompressedMessage = originalMessage.Compress().Decompress();

            // Assert
            Assert.That(decompressedMessage.position.y, Is.GreaterThanOrEqualTo(0));
            Assert.AreEqual(Mathf.Clamp(originalMessage.position.y, 0, CompressionConfig.Y_MAX), decompressedMessage.position.y, quantizationError);

            Debug.Log($"Y quantization error = {quantizationError} | original: {originalMessage.position.y} | decompressed: {decompressedMessage.position.y}");
        }

        [TestCase(0, 0, 0)]
        [TestCase(1.153f, 2.753f, 3.523f)]
        [TestCase(-1.153f, -2.753f, -3.523f)]
        [TestCase(5.153f, 10f, 15.523f)]
        [TestCase(-5.153f, -10f, -15.523f)]
        [TestCase(20.241f, 30f, CompressionConfig.MAX_VELOCITY - 0.003f)]
        [TestCase(-20.241f, -30f, -CompressionConfig.MAX_VELOCITY + 0.023f)]
        [TestCase(CompressionConfig.MAX_VELOCITY / 2f, CompressionConfig.MAX_VELOCITY / 2f, CompressionConfig.MAX_VELOCITY / 2f)]
        [TestCase(CompressionConfig.MAX_VELOCITY, -CompressionConfig.MAX_VELOCITY, CompressionConfig.MAX_VELOCITY)]
        [TestCase(-CompressionConfig.MAX_VELOCITY, CompressionConfig.MAX_VELOCITY, -CompressionConfig.MAX_VELOCITY)]
        public void ShouldCorrectlyCompressAndDecompressVelocity(float x, float y, float z)
        {
            // Arrange
            float stepSize = 2 * CompressionConfig.MAX_VELOCITY / Mathf.Pow(2, CompressionConfig.VELOCITY_BITS);
            float quantizationError = (stepSize / 2f) + 0.01f; // there is a small deviation at zero (< 0.01f)

            var originalMessage = new NetworkMovementMessage { velocity = new Vector3(x, y, z) };

            // Act
            NetworkMovementMessage decompressedMessage = originalMessage.Compress().Decompress();

            // Assert
            Assert.AreEqual(originalMessage.velocity.x, decompressedMessage.velocity.x, quantizationError);
            Assert.AreEqual(originalMessage.velocity.y, decompressedMessage.velocity.y, quantizationError);
            Assert.AreEqual(originalMessage.velocity.z, decompressedMessage.velocity.z, quantizationError);

            Debug.Log($" Velocity quantization error = {quantizationError} | original: {originalMessage.velocity} | decompressed: {decompressedMessage.velocity}");
        }
    }
}
