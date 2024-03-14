using NUnit.Framework;
using TimestampEncodingTests;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class MovementMessageCompressionTests
    {
        // [TestCase(0.1f, true, 1.525f, 20.3f, 1.575f)]
        // [TestCase(9.01f, true, 3.05f, 18.03f, 8.125f)]
        // [TestCase(0.255f, false, 15.5f, 32.5f, 15.5f)]
        public void EncodeDecodePositionTest(float timestamp, bool isMoving, float x, float y, float z)
        {
            long encoded = Encoder.Encode(isMoving, timestamp, x, y, z);
            (bool decodedIsMoving, float decodedTimestamp, float decodedX, float decodedY, float decodedZ) = Encoder.Decode(encoded);

            Debug.Log($"{x} | {y} | {z}");
            Debug.Log($"{decodedX} | {decodedY} | {decodedZ}");

            const float ErrorMargin = TimestampEncoder.QUANTUM; // Adjust based on expected precision

            Assert.That(decodedTimestamp, Is.InRange(timestamp - ErrorMargin, timestamp + ErrorMargin));
            Assert.AreEqual(isMoving, decodedIsMoving);
            Assert.That(decodedX, Is.InRange(x - ErrorMargin, x + ErrorMargin));
            Assert.That(decodedY, Is.InRange(y - ErrorMargin, y + ErrorMargin));
            Assert.That(decodedZ, Is.InRange(z - ErrorMargin, z + ErrorMargin));
        }
    }
}
