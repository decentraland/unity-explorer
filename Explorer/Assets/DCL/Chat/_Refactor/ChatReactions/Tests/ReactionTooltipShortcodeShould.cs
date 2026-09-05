using DCL.Chat.ChatReactions.Core;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ReactionTooltipShortcodeShould
    {
        [TestCase(0x1F1E6u, ":letter-A:")]
        [TestCase(0x1F1E9u, ":letter-D:")]
        [TestCase(0x1F1EAu, ":letter-E:")]
        [TestCase(0x1F1FFu, ":letter-Z:")]
        public void MapRegionalIndicatorToLetterShortcode(uint unicode, string expected)
        {
            string? result = EmojiCodepointHelper.TryGetRegionalIndicatorShortcode(unicode);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(0x1F1E5u, Description = "One below range")]
        [TestCase(0x1F200u, Description = "Above range")]
        [TestCase(0x1F600u, Description = "Smiley face")]
        [TestCase(0u, Description = "Zero")]
        public void ReturnNullForNonRegionalIndicator(uint unicode)
        {
            string? result = EmojiCodepointHelper.TryGetRegionalIndicatorShortcode(unicode);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void MapAllTwentySixLetters()
        {
            for (uint i = 0; i < 26; i++)
            {
                uint unicode = 0x1F1E6 + i;
                char expectedLetter = (char)('A' + i);

                string? result = EmojiCodepointHelper.TryGetRegionalIndicatorShortcode(unicode);
                Assert.That(result, Is.EqualTo($":letter-{expectedLetter}:"),
                    $"Failed for U+{unicode:X4} (expected letter {expectedLetter})");
            }
        }
    }
}
