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
            int wire = ReactionWireEncoding.Encode(5, isRemoval: false);

            Assert.That(wire, Is.EqualTo(5));
        }

        [Test]
        public void EncodeRemovalAsNegative()
        {
            int wire = ReactionWireEncoding.Encode(5, isRemoval: true);

            Assert.That(wire, Is.LessThan(0));
            Assert.That(wire, Is.EqualTo(~5));
        }

        [Test]
        public void DecodePositiveAsAdd()
        {
            var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(5);

            Assert.That(emojiIndex, Is.EqualTo(5));
            Assert.That(isRemoval, Is.False);
        }

        [Test]
        public void DecodeNegativeAsRemoval()
        {
            var (emojiIndex, isRemoval) = ReactionWireEncoding.Decode(~5);

            Assert.That(emojiIndex, Is.EqualTo(5));
            Assert.That(isRemoval, Is.True);
        }

        [Test]
        public void RoundTripAddPreservesIndex()
        {
            int original = 42;
            int wire = ReactionWireEncoding.Encode(original, isRemoval: false);
            var (decoded, isRemoval) = ReactionWireEncoding.Decode(wire);

            Assert.That(decoded, Is.EqualTo(original));
            Assert.That(isRemoval, Is.False);
        }

        [Test]
        public void RoundTripRemovalPreservesIndex()
        {
            int original = 42;
            int wire = ReactionWireEncoding.Encode(original, isRemoval: true);
            var (decoded, isRemoval) = ReactionWireEncoding.Decode(wire);

            Assert.That(decoded, Is.EqualTo(original));
            Assert.That(isRemoval, Is.True);
        }

        [Test]
        public void HandleZeroEmojiIndex()
        {
            int addWire = ReactionWireEncoding.Encode(0, isRemoval: false);
            int removeWire = ReactionWireEncoding.Encode(0, isRemoval: true);

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
