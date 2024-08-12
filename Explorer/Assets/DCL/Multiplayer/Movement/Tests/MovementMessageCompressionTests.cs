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
        public void CompressAndDecompress_YPosition_ShouldReturnOriginalValue(float x, float y, float z)
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
    }
}
