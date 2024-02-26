using NUnit.Framework;
using System;
using TimestampEncodingTests;

namespace DCL.Multiplayer.Movement.Tests
{
    [TestFixture]
    public class MovementMessageCompressionTests
    {
        [Test]
        [TestCase(0.1f, true)]
        [TestCase(0.255f, false)] // Test edge case of max 8-bit value
        public void EncodeDecodeTimestampTest(float expectedTimestamp, bool isMoving)
        {
            // Act
            long encoded = TimestampEncoder.Encode(expectedTimestamp, isMoving);
            (float decodedTimestamp, bool decodedState) = TimestampEncoder.Decode(encoded);

            // Assert
            const float ERROR_MARGIN = TimestampEncoder.QUANTUM;
            bool timestampWithinErrorMargin = Math.Abs(decodedTimestamp - expectedTimestamp) < ERROR_MARGIN;

            Assert.IsTrue(timestampWithinErrorMargin, $"Decoded timestamp {decodedTimestamp} is not within error margin of {ERROR_MARGIN} seconds from the expected timestamp {expectedTimestamp}.");
            Assert.AreEqual(isMoving, decodedState, "Movement state was not correctly encoded/decoded.");
        }
    }
}
