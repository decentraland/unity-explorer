using DCL.Chat.ChatReactions.Networking;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ReactionWireEncodingShould
    {
        [Test]
        public void EncodeAddAsPositive()
        {
            // Act
            int wire = ReactionWireEncoding.Encode(5, isRemoval: false);

            // Assert
            Assert.That(wire, Is.EqualTo(5));
        }

        [Test]
        public void EncodeRemovalAsNegative()
        {
            // Act
            int wire = ReactionWireEncoding.Encode(5, isRemoval: true);

            // Assert
            Assert.That(wire, Is.LessThan(0));
            Assert.That(wire, Is.EqualTo(~5));
        }

        [Test]
        public void DecodePositiveAsAdd()
        {
            // Act
            var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(5);

            // Assert
            Assert.That(emojiIndex, Is.EqualTo(5));
            Assert.That(isRemoval, Is.False);
        }

        [Test]
        public void DecodeNegativeAsRemoval()
        {
            // Act
            var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(~5);

            // Assert
            Assert.That(emojiIndex, Is.EqualTo(5));
            Assert.That(isRemoval, Is.True);
        }

        [Test]
        public void RoundTripAddPreservesIndex()
        {
            // Arrange
            int original = 42;

            // Act
            int wire = ReactionWireEncoding.Encode(original, isRemoval: false);
            var (decoded, isRemoval) = ReactionWireEncoding.Decode(wire);

            // Assert
            Assert.That(decoded, Is.EqualTo(original));
            Assert.That(isRemoval, Is.False);
        }

        [Test]
        public void RoundTripRemovalPreservesIndex()
        {
            // Arrange
            int original = 42;

            // Act
            int wire = ReactionWireEncoding.Encode(original, isRemoval: true);
            var (decoded, isRemoval) = ReactionWireEncoding.Decode(wire);

            // Assert
            Assert.That(decoded, Is.EqualTo(original));
            Assert.That(isRemoval, Is.True);
        }

        // Verifies index 0 works correctly since the bitwise complement of 0 is -1, a common edge case.
        [Test]
        public void HandleZeroEmojiIndex()
        {
            // Act
            int addWire = ReactionWireEncoding.Encode(0, isRemoval: false);
            int removeWire = ReactionWireEncoding.Encode(0, isRemoval: true);

            // Assert
            Assert.That(addWire, Is.EqualTo(0));
            Assert.That(removeWire, Is.EqualTo(~0));

            var (addIndex, addIsRemoval) = ReactionWireEncoding.Decode(addWire);
            var (removeIndex, removeIsRemoval) = ReactionWireEncoding.Decode(removeWire);

            Assert.That(addIndex, Is.EqualTo(0));
            Assert.That(addIsRemoval, Is.False);
            Assert.That(removeIndex, Is.EqualTo(0));
            Assert.That(removeIsRemoval, Is.True);
        }
    }
}
