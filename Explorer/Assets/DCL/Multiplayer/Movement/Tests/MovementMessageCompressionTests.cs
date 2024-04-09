using NUnit.Framework;
using TimestampEncodingTests;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests
{

    public class MovementMessageCompressionTests
    {
        //
        //
        //
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
