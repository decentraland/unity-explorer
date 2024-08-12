using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.Systems;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class MovementMessageCompressionTests
    {
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
