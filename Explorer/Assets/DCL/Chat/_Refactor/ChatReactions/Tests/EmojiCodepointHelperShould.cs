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
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint("A", out uint codepoint);

            Assert.That(result, Is.True);
            Assert.That(codepoint, Is.EqualTo(0x41));
        }

        [Test]
        public void ExtractSurrogatePairCodepoint()
        {
            // U+1F600 = 😀 (grinning face) — requires surrogate pair in UTF-16
            string emoji = char.ConvertFromUtf32(0x1F600);

            bool result = EmojiCodepointHelper.TryGetSingleCodepoint(emoji, out uint codepoint);

            Assert.That(result, Is.True);
            Assert.That(codepoint, Is.EqualTo(0x1F600u));
        }

        [Test]
        public void ReturnFalseForEmptyString()
        {
            bool result = EmojiCodepointHelper.TryGetSingleCodepoint("", out uint codepoint);

            Assert.That(result, Is.False);
            Assert.That(codepoint, Is.EqualTo(0u));
        }

        [Test]
        public void ReturnFalseForHighSurrogateWithoutLow()
        {
            // Isolated high surrogate (no low surrogate following)
            string broken = "\uD83D";

            bool result = EmojiCodepointHelper.TryGetSingleCodepoint(broken, out uint codepoint);

            Assert.That(result, Is.False);
        }

        [Test]
        public void ConvertBmpCodepointToString()
        {
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x41);

            Assert.That(result, Is.EqualTo("A"));
        }

        [Test]
        public void ConvertSupplementaryCodepointToString()
        {
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x1F600);
            string expected = char.ConvertFromUtf32(0x1F600);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ReturnQuestionMarkForSurrogateRange()
        {
            // Bare surrogate codepoint (invalid)
            string result = EmojiCodepointHelper.CodepointToDisplayString(0xD800);

            Assert.That(result, Is.EqualTo("?"));
        }

        [Test]
        public void ReturnQuestionMarkForOutOfRange()
        {
            string result = EmojiCodepointHelper.CodepointToDisplayString(0x110000);

            Assert.That(result, Is.EqualTo("?"));
        }
    }
}
