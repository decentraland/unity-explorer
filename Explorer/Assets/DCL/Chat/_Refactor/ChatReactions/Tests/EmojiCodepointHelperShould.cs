using DCL.Chat.ChatReactions.Core;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class EmojiCodepointHelperShould
    {
        [Test]
        public void ExtractBmpCodepoint()
        {
            // Act
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint("A", out uint codepoint);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(codepoint, Is.EqualTo(0x41));
        }

        [Test]
        public void ExtractSurrogatePairCodepoint()
        {
            // Arrange — U+1F600 = grinning face, requires surrogate pair in UTF-16
            string emoji = char.ConvertFromUtf32(0x1F600);

            // Act
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint(emoji, out uint codepoint);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(codepoint, Is.EqualTo(0x1F600u));
        }

        [Test]
        public void ReturnFalseForEmptyString()
        {
            // Act
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint("", out uint codepoint);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(codepoint, Is.EqualTo(0u));
        }

        [Test]
        public void ReturnFalseForHighSurrogateWithoutLow()
        {
            // Arrange — isolated high surrogate (no low surrogate following)
            string broken = "\uD83D";

            // Act
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint(broken, out uint codepoint);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ConvertBmpCodepointToString()
        {
            // Act
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x41);

            // Assert
            Assert.That(result, Is.EqualTo("A"));
        }

        [Test]
        public void ConvertSupplementaryCodepointToString()
        {
            // Act
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x1F600);

            // Assert
            string expected = char.ConvertFromUtf32(0x1F600);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ReturnQuestionMarkForSurrogateRange()
        {
            // Act — bare surrogate codepoint (invalid)
            string result = EmojiCodepointHelper.CodepointToDisplayString(0xD800);

            // Assert
            Assert.That(result, Is.EqualTo("?"));
        }

        [Test]
        public void ReturnQuestionMarkForOutOfRange()
        {
            // Act
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x110000);

            // Assert
            Assert.That(result, Is.EqualTo("?"));
        }
    }
}
